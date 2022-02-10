using Microsoft.AspNetCore.Authorization;
using System;
using TranslateServer.Model;

namespace TranslateServer
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class AuthAdminAttribute : AuthorizeAttribute
    {
        public AuthAdminAttribute()
        {
            Roles = UserDocument.ADMIN;
        }
    }
}
