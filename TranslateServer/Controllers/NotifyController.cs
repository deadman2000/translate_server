using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Services;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NotifyController : ApiController
    {
        private readonly CommentNotifyService _commentNotify;
        private readonly CommentsService _comments;
        private readonly TranslateService _translate;
        private readonly UsersService _users;

        public NotifyController(CommentNotifyService commentNotify, CommentsService comments, TranslateService translate, UsersService users)
        {
            _commentNotify = commentNotify;
            _comments = comments;
            _translate = translate;
            _users = users;
        }

        [HttpGet]
        public async Task<ActionResult> Get()
        {
            var notifies = _commentNotify.Queryable()
                .Where(n => n.User == UserLogin)
                .OrderByDescending(n => n.Date);

            var commIds = notifies.Select(n => n.CommentId).ToArray();
            var cursor = await _comments.Collection.FindAsync(Builders<Comment>.Filter.In(n => n.Id, commIds));
            var comments = await cursor.ToListAsync();
            var commById = comments.ToDictionary(c => c.Id, c => c);
            List<object> list = new();
            foreach (var notify in notifies)
            {
                if (!commById.TryGetValue(notify.CommentId, out var comm)) // Удаляем уведомление для удалённого комментария
                {
                    await _commentNotify.DeleteOne(n => n.Id == notify.Id);
                    continue;
                }

                list.Add(new
                {
                    notify.Id,
                    notify.Date,
                    notify.Read,
                    comm.Project,
                    comm.Volume,
                    comm.Number,
                    comm.Text,
                    comm.Author
                });
            }

            return Ok(list);
        }

        [HttpPost("read")]
        public async Task<ActionResult> Read()
        {
            await _commentNotify.Update(n => n.User == UserLogin && !n.Read)
                .Set(n => n.Read, true)
                .ExecuteMany();
            return Ok();
        }

        //[AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<ActionResult> Refresh()
        {
            // Comments fix
            {
                var comments = await _comments.Query(c => c.Number == null);
                foreach (var comm in comments)
                {
                    var tr = await _translate.Get(t => t.Id == comm.TranslateId);
                    if (tr == null)
                    {
                        await _comments.DeleteOne(c => c.Id == comm.Id);
                        continue;
                    }

                    await _comments.Update(c => c.Id == comm.Id)
                        .Set(c => c.Number, tr.Number)
                        .Execute();
                }
            }

            // Create notifies
            {
                var admins = (await _users.Query(u => u.Role == UserDocument.ADMIN)).Select(u => u.Login).ToList();
                var comments = await _comments.All();
                foreach (var comm in comments)
                {
                    var notify = await _commentNotify.Get(n => n.CommentId == comm.Id);
                    if (notify != null)
                        continue;

                    var tr = await _translate.Get(t => t.Id == comm.TranslateId);

                    // Отправляем уведомление автору перевода, если комментировал не он
                    if (comm.Author != tr.Author)  // Не отправляем себе
                    {
                        await _commentNotify.Insert(new CommentNotify
                        {
                            CommentId = comm.Id,
                            Date = comm.DateCreate,
                            User = tr.Author
                        });
                    }

                    // Находим всех, кто комментировал
                    var users = _comments.Queryable().Where(c => c.TranslateId == tr.Id)
                        .Select(c => c.Author)
                        .Distinct()
                        .ToList();

                    // Отправляем уведомление другим комментировавшим, если он на автор комментария
                    foreach (var user in users)
                    {
                        if (comm.Author == user) continue; // Не отправляем себе
                        if (tr.Author == user) continue; // Автору перевода мы уже отправили уведомление выше

                        await _commentNotify.Insert(new CommentNotify
                        {
                            CommentId = comm.Id,
                            Date = comm.DateCreate,
                            User = user
                        });
                    }

                    // Отправляем уведомление админам
                    foreach (var user in admins)
                    {
                        if (comm.Author == user) continue; // Не отправляем себе
                        if (tr.Author == user) continue; // Автору перевода мы уже отправили уведомление выше
                        if (users.Contains(user)) continue; // Участнику обсуждения отправляли выше

                        await _commentNotify.Insert(new CommentNotify
                        {
                            CommentId = comm.Id,
                            Date = comm.DateCreate,
                            User = user
                        });
                    }
                }
            }

            return Ok();
        }
    }
}
