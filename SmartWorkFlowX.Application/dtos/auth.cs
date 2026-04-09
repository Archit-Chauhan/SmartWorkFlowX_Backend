using System;
using System.Collections.Generic;
using System.Text;

namespace SmartWorkFlowX.Application.dtos
{
  public class RegisterDto
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }
    public class LoginDto
    {
        public string Email { get; set; }
        public string Password { get; set; }

    }
}
