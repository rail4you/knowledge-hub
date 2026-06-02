using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Volo.Abp.AspNetCore.Mvc;

namespace KnowledgeHub.Controllers;

[Area("app")]
[Route("api/app/logout")]
public class LogoutController : AbpController
{
    /// <summary>
    /// 清除 IdP session cookie 并登出
    /// Cookie 在 API 域名（localhost:44305）下设置，需要从 API 端清除
    /// </summary>
    [HttpGet("clear-session")]
    public IActionResult ClearSession()
    {
        // 清除 OpenIddict session cookie（Set-Cookie 的方式）
        Response.Cookies.Append("idsrv", "", new CookieOptions
        {
            Path = "/",
            Expires = System.DateTimeOffset.UtcNow.AddYears(-1),
            HttpOnly = true,
            SameSite = SameSiteMode.None,
            Secure = true
        });

        Response.Cookies.Append("idsrv.session", "", new CookieOptions
        {
            Path = "/",
            Expires = System.DateTimeOffset.UtcNow.AddYears(-1),
            HttpOnly = true,
            SameSite = SameSiteMode.None,
            Secure = true
        });

        Response.Cookies.Append(".AspNetCore.Identity.Application", "", new CookieOptions
        {
            Path = "/",
            Expires = System.DateTimeOffset.UtcNow.AddYears(-1),
            HttpOnly = true,
            SameSite = SameSiteMode.Strict
        });

        Response.Cookies.Append(".AspNetCore.Session", "", new CookieOptions
        {
            Path = "/",
            Expires = System.DateTimeOffset.UtcNow.AddYears(-1),
            SameSite = SameSiteMode.Strict
        });

        Response.Cookies.Append("refreshToken", "", new CookieOptions
        {
            Path = "/",
            Expires = System.DateTimeOffset.UtcNow.AddYears(-1)
        });

        return Ok(new { message = "Session cleared" });
    }
}