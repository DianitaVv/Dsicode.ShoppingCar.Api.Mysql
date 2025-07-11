using Microsoft.AspNetCore.Authentication;
using System.Net.Http.Headers;

namespace Dsicode.ShoppingCart.API.Utility
{
    public class BackendApiAuthenticationHttpClientHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _accessor;

        public BackendApiAuthenticationHttpClientHandler(IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                // ✅ Solo intentar obtener token si hay un contexto HTTP válido
                if (_accessor.HttpContext != null)
                {
                    // ✅ Intentar obtener el token del contexto de autenticación
                    var token = await _accessor.HttpContext.GetTokenAsync("access_token");

                    // ✅ Si no hay token en GetTokenAsync, intentar obtenerlo del header Authorization
                    if (string.IsNullOrEmpty(token))
                    {
                        var authHeader = _accessor.HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                        {
                            token = authHeader.Substring("Bearer ".Length).Trim();
                        }
                    }

                    // ✅ Si tenemos token, agregarlo al request
                    if (!string.IsNullOrEmpty(token))
                    {
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                        Console.WriteLine($"🔑 Token agregado para comunicación entre servicios: {token.Substring(0, Math.Min(20, token.Length))}...");
                    }
                    else
                    {
                        Console.WriteLine("⚠️ No se encontró token para comunicación entre servicios");
                    }
                }
            }
            catch (Exception ex)
            {
                // ✅ Log del error pero continúa para endpoints públicos
                Console.WriteLine($"⚠️ Error obteniendo token para comunicación entre servicios: {ex.Message}");
            }

            // ✅ Agregar headers adicionales para debugging
            request.Headers.Add("User-Agent", "ShoppingCart-API/1.0");
            request.Headers.Add("X-Forwarded-Service", "ShoppingCart");

            try
            {
                var response = await base.SendAsync(request, cancellationToken);

                // ✅ Log para debugging
                Console.WriteLine($"📡 Request a {request.RequestUri}: {response.StatusCode}");

                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en comunicación entre servicios: {ex.Message}");
                throw;
            }
        }
    }
}