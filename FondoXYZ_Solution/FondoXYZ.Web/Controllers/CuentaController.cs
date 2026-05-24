using FondoXYZ.Domain.Entities;
using FondoXYZ.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Mail;

namespace FondoXYZ.Web.Controllers
{
    public class CuentaController : Controller
    {
        private readonly UserManager<Usuario> _userManager;
        private readonly SignInManager<Usuario> _signInManager;
        private readonly IConfiguration _configuration;

        public CuentaController(UserManager<Usuario> userManager, SignInManager<Usuario> signInManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
        }

        // ==========================================
        // 1. LOGIN
        // ==========================================
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string nroDocumento, string clave)
        {
            if (string.IsNullOrEmpty(nroDocumento) || string.IsNullOrEmpty(clave))
            {
                ViewBag.Error = "Debe ingresar documento y contraseña.";
                return View();
            }

            // Usar SignInManager de Identity. NroDocumento es el UserName.
            var result = await _signInManager.PasswordSignInAsync(nroDocumento, clave, isPersistent: true, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                return RedirectToAction("Index", "Home");
            }
            
            ViewBag.Error = "Documento o contraseña incorrectos.";
            return View();
        }

        // ==========================================
        // 2. LOGOUT
        // ==========================================
        public async Task<IActionResult> Salir()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Cuenta");
        }

        // ==========================================
        // 3. REGISTRO
        // ==========================================
        [HttpGet]
        public IActionResult Registro()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registro(Usuario modelo, string clave, string direccionEmail, string celular) 
        {
            // Mapeamos los campos que la vista manda con los nombres anteriores a Identity
            modelo.Email = direccionEmail;
            modelo.PhoneNumber = celular;
            modelo.UserName = modelo.NroDocumento;
            modelo.FechaRegistro = DateTime.Now;

            // Validar si el documento ya existe
            var userExists = await _userManager.FindByNameAsync(modelo.NroDocumento ?? "");
            if (userExists != null)
            {
                ViewBag.Error = "El número de documento ya se encuentra registrado.";
                return View(modelo);
            }

            var emailExists = await _userManager.FindByEmailAsync(modelo.Email ?? "");
            if (emailExists != null)
            {
                ViewBag.Error = "El correo electrónico ya se encuentra registrado.";
                return View(modelo);
            }

            // Ignorar validaciones del modelo sobre campos de Identity
            ModelState.Remove("PasswordHash");
            ModelState.Remove("SecurityStamp");
            ModelState.Remove("Email");

            if (ModelState.IsValid)
            {
                var result = await _userManager.CreateAsync(modelo, clave);
                
                if (result.Succeeded)
                {
                    TempData["MensajeExito"] = "Registro exitoso. Ahora puede iniciar sesión.";
                    return RedirectToAction("Login");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                        ViewBag.Error += error.Description + " ";
                    }
                }
            }

            return View(modelo);
        }

        // ==========================================
        // 4. RECUPERAR CONTRASEÑA (SMTP)
        // ==========================================
        [HttpGet]
        public IActionResult RecuperarClave()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecuperarClave(string email)
        {
            var usuario = await _userManager.FindByEmailAsync(email);

            if (usuario == null)
            {
                ViewBag.Error = "No se encontró ningún usuario con ese correo electrónico.";
                return View();
            }

            // Generar una contraseña temporal aleatoria de 4 números según la regla de la vista
            string nuevaClaveTemporal = new Random().Next(1000, 9999).ToString();

            // Reseteamos el password usando el token de Identity
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(usuario);
            var result = await _userManager.ResetPasswordAsync(usuario, resetToken, nuevaClaveTemporal);

            if (result.Succeeded)
            {
                bool correoEnviado = EnviarCorreoSmtp(usuario.Email ?? "", usuario.NombreCompleto, nuevaClaveTemporal);

                if (correoEnviado)
                {
                    ViewBag.Exito = "Se ha enviado una nueva contraseña temporal a su correo.";
                }
                else
                {
                    ViewBag.Error = "Hubo un error al intentar enviar el correo. Verifique la configuración SMTP.";
                }
            }
            else
            {
                ViewBag.Error = "Error al reiniciar la contraseña.";
            }

            return View();
        }

        // ==========================================
        // MÉTODOS PRIVADOS AUXILIARES
        // ==========================================
        private bool EnviarCorreoSmtp(string destinatario, string nombre, string nuevaClave)
        {
            try
            {
                string smtpServer = _configuration["SmtpSettings:Server"] ?? throw new ArgumentNullException("SmtpSettings:Server no configurado");
                string portString = _configuration["SmtpSettings:Port"] ?? throw new ArgumentNullException("SmtpSettings:Port no configurado");
                string senderEmail = _configuration["SmtpSettings:SenderEmail"] ?? throw new ArgumentNullException("SmtpSettings:SenderEmail no configurado");
                string senderName = _configuration["SmtpSettings:SenderName"] ?? "Fondo XYZ Reservas";
                string password = _configuration["SmtpSettings:Password"] ?? throw new ArgumentNullException("SmtpSettings:Password no configurado");

                if (!int.TryParse(portString, out int smtpPort))
                {
                    throw new FormatException("El puerto SMTP configurado no es un número válido.");
                }

                MailMessage correo = new MailMessage();
                correo.From = new MailAddress(senderEmail, senderName);
                correo.To.Add(destinatario);
                correo.Subject = "Recuperación de Contraseña - Fondo XYZ";
                correo.Body = $"Hola {nombre},<br><br>Su nueva contraseña temporal es: <b>{nuevaClave}</b><br><br>Le recomendamos cambiarla una vez ingrese al sistema.";
                correo.IsBodyHtml = true;

                using (SmtpClient cliente = new SmtpClient(smtpServer, smtpPort))
                {
                    cliente.Credentials = new NetworkCredential(senderEmail, password);
                    cliente.EnableSsl = true;
                    cliente.Send(correo);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}