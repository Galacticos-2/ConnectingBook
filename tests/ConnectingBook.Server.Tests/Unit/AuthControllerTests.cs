using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Project_Web_ConnectingBook.Server.Controllers;

namespace ConnectingBook.Server.Tests.Unit
{
    public class AuthControllerTests
    {
        [Fact]
        public async Task Missing_empty_or_oversized_cookie_returns_401_without_throwing_or_using_auth_service()
        {
            foreach (var cookie in new string?[] { null, "", new string('a', 129) })
            {
                var context = new DefaultHttpContext();
                context.Request.Path = "/api/auth/refresh";
                if (cookie is not null) context.Request.Headers.Cookie = "connectingbook.refresh=" + cookie;
                var controller = new AuthController(null!, new ConfigurationBuilder().Build())
                {
                    ControllerContext = new ControllerContext { HttpContext = context }
                };
                var response = await controller.Refresh(CancellationToken.None);
                var result = Assert.IsType<ObjectResult>(response.Result);
                Assert.Equal(401, result.StatusCode);
                Assert.Contains("application/problem+json", result.ContentTypes);
                var problem = Assert.IsType<ProblemDetails>(result.Value);
                Assert.Equal("invalid_session", problem.Extensions["code"]);
                Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
                var logout = Assert.IsType<ObjectResult>(await controller.Logout(CancellationToken.None));
                Assert.Equal(401, logout.StatusCode);
                Assert.Contains("connectingbook.refresh=", context.Response.Headers.SetCookie.ToString());
            }
        }
    }
}
