using MediatR;
using SkyScan.Core.Entities;
using System;

namespace SkyScan.Application.Account.DeleteAccount
{
    public class DeleteAccountCommand : IRequest<AuthResult>
    {
        public User User { get; set; }
        public string Password { get; set; }
    }
}
