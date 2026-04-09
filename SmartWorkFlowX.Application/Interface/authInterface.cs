using SmartWorkFlowX.Application.dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartWorkFlowX.Application.Interface
{
    public interface IAuthService {
        Task<string> RegisterAsync(RegisterDto dto);
        Task<string> LoginAsync(LoginDto dto);
    
    }
}
