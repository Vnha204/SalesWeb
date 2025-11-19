using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SalesWeb.Data;
using System;
using System.Linq;

namespace SalesWeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        public AccountController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        public IActionResult Info()
        {
            int? userId = HttpContext.Session.GetInt32("CurrentUserId");

            if (userId == null)
                return RedirectToAction("Index", "Login");

            var user = _context.Users.FirstOrDefault(u => u.user_id == userId);
            if (user == null)
                return RedirectToAction("Index", "Login");

            return View(user);
        }

        [HttpGet]
        public JsonResult GetMonthlySales()
        {
            int? userId = HttpContext.Session.GetInt32("CurrentUserId");
            if (userId == null) return Json(new { success = false, message = "Chưa đăng nhập" });

            var year = DateTime.Now.Year;
            var sales = _context.Invoices
                .Where(i => i.SellerId == userId && i.Status == "completed" && i.InvoiceDate.Year == year)
                .GroupBy(i => i.InvoiceDate.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    Total = g.Sum(x => x.TotalAmount)
                })
                .ToList();

            // Bổ sung tháng không có doanh thu = 0
            var result = Enumerable.Range(1, 12)
                .Select(m => new
                {
                    Month = m,
                    Total = sales.FirstOrDefault(s => s.Month == m)?.Total ?? 0
                });

            return Json(new { success = true, data = result });
        }
        [HttpGet]
        public IActionResult GetPersonalKPI()
        {
            int? userId = HttpContext.Session.GetInt32("CurrentUserId");
            string? role = HttpContext.Session.GetString("UserRole");

            if (userId == null)
                return Json(new { success = false, message = "Chưa đăng nhập." });

            // Ẩn phần KPI cho quản lý
            if (role == "manager")
                return Json(new { success = false, message = "Quản lý không có KPI cá nhân." });

            var connStr = _config.GetConnectionString("DefaultConnection");

            decimal target = 0, achieved = 0;
            string evaluation = "";
            int month = DateTime.Now.Month;
            int year = DateTime.Now.Year;

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();

                // Lấy KPI tháng hiện tại
                string sql = @"
                    SELECT ISNULL(target_sales,0), ISNULL(achieved_sales,0), 
                           ISNULL(evaluation, N'Chưa có nhận xét')
                    FROM dbo.KPI
                    WHERE user_id=@uid AND [month]=@month AND [year]=@year";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@uid", userId);
                    cmd.Parameters.AddWithValue("@month", month);
                    cmd.Parameters.AddWithValue("@year", year);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            target = reader.GetDecimal(0);
                            achieved = reader.GetDecimal(1);
                            evaluation = reader.GetString(2);
                        }
                    }
                }

                // Nếu chưa có KPI được giao
                if (target == 0 && achieved == 0)
                {
                    return Json(new { success = false, message = "Chưa có KPI được giao trong tháng này." });
                }

                decimal percent = target > 0 ? Math.Round((achieved / target) * 100, 1) : 0;
                return Json(new { success = true, target, achieved, percent, evaluation });
            }
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete("username");
            Response.Cookies.Delete("password");
            return RedirectToAction("Index", "Login");
        }
    }
}
