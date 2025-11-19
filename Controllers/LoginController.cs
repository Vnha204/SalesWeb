using Microsoft.AspNetCore.Mvc;
using SalesWeb.Data;
using SalesWeb.Models;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace SalesWeb.Controllers
{
    public class LoginController : Controller
    {
        private readonly AppDbContext _context;

        public LoginController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            string? savedUsername = Request.Cookies["username"];
            string? savedPassword = Request.Cookies["password"];

            ViewBag.SavedUsername = savedUsername ?? "";
            ViewBag.SavedPassword = savedPassword ?? "";
            ViewBag.RememberChecked = savedUsername != null ? "checked" : "";

            return View();
        }

        [HttpPost]
        public IActionResult Index(string username, string password, bool remember)
        {
            var user = _context.Users.FirstOrDefault(u => u.username == username);

            if (user == null)
            {
                TempData["Error"] = "Tài khoản không tồn tại!";
                return RedirectToAction("Index");
            }

            if (user.password_hash != password)
            {
                TempData["Error"] = "Sai mật khẩu!";
                return RedirectToAction("Index");
            }
            string role = user.role == "sales" ? "staff" : user.role;


            // Lưu thông tin đăng nhập vào Session
            HttpContext.Session.SetString("UserName", user.full_name);
            HttpContext.Session.SetString("UserRole", user.role);
            HttpContext.Session.SetInt32("CurrentUserId", user.user_id);

            if (remember)
            {
                Response.Cookies.Append("username", username, new CookieOptions { Expires = DateTime.Now.AddDays(7) });
                Response.Cookies.Append("password", password, new CookieOptions { Expires = DateTime.Now.AddDays(7) });
            }
            else
            {
                Response.Cookies.Delete("username");
                Response.Cookies.Delete("password");
            }

            if (user.role == "manager")
            {
                return RedirectToAction("Index", "Home"); 
            }
            else if (user.role == "sales")
            {
                return RedirectToAction("Index", "Home"); 
            }
            else
            {
                TempData["Error"] = "Vai trò không hợp lệ!";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            Response.Cookies.Delete("username");
            Response.Cookies.Delete("password");

            return RedirectToAction("Index");
        }
    }
}
