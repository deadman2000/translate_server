using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SCI_Lib;
using SCI_Lib.Resources;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TranslateServer.Store;

namespace TranslateServer.Services
{
    public class ExternalApprover
    {
        private readonly string _project;
        private readonly IServiceProvider _serviceProvider;
        private readonly HashSet<string> _approved = new();

        private SCIPackage _package;

        public ExternalApprover(IServiceProvider serviceProvider, IConfiguration config)
        {
            _serviceProvider = serviceProvider;
            _project = config["Approve"];
        }

        public async Task ApproveMessage(ushort res, byte noun, byte verb)
        {
            if (_package == null) LoadPackage();

            string code = $"{res}.{noun}.{verb}";
            if (_approved.Contains(code)) return;

            var msg = _package.GetResource<ResMessage>(res);
            var ind = msg.GetMessages().FindIndex(m => m.Noun == noun && m.Verb == verb);

            if (ind >= 0)
            {
                using var scope = _serviceProvider.CreateScope();
                var texts = scope.ServiceProvider.GetRequiredService<TextsStore>();
                await texts.Update()
                    .Where(t => t.Project == _project && t.Volume == $"{res}_msg" && t.Number == ind)
                    .Set(t => t.TranslateApproved, true)
                    .Execute();
            }

            _approved.Add(code);
        }

        private void LoadPackage()
        {
            using var scope = _serviceProvider.CreateScope();
            var sci = scope.ServiceProvider.GetRequiredService<SCIService>();
            _package = sci.Load(_project);
        }
    }
}
