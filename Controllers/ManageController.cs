using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using SalesWeb.Models;
using System;

namespace SalesWeb.Controllers
{
    public class ManageController : Controller
    {
        private readonly IConfiguration _config;

        public ManageController(IConfiguration config)
        {
            _config = config;
        }

        public IActionResult Index()
        {
            return View();
        }

        //Lấy danh sách nhân viên + KPI tháng hiện tại
        [HttpGet]
        public JsonResult GetEmployeesWithKPI(int? month = null, int? year = null)
        {
            var connStr = _config.GetConnectionString("DefaultConnection");
            var employees = new List<object>();

            int currentMonth = month ?? DateTime.Now.Month;
            int currentYear = year ?? DateTime.Now.Year;

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();

                string sql = @"
                        SELECT 
                            u.user_id, 
                            u.full_name, 
                            u.position, 
                            ISNULL(k.target_sales, 0) AS target_sales,
                            ISNULL(k.evaluation, N'Chưa có nhận xét') AS evaluation,
                            ISNULL(SUM(i.total_amount), 0) AS achieved_sales
                        FROM dbo.Users u
                        LEFT JOIN dbo.KPI k 
                            ON u.user_id = k.user_id 
                            AND k.[month] = @month 
                            AND k.[year] = @year
                        LEFT JOIN dbo.Invoices i 
                            ON i.seller_id = u.user_id 
                            AND i.status = 'completed'
                            AND MONTH(i.invoice_date) = @month
                            AND YEAR(i.invoice_date) = @year
                        WHERE u.role = 'sales'
                        GROUP BY u.user_id, u.full_name, u.position, k.target_sales, k.evaluation
                        ORDER BY u.full_name";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@month", currentMonth);
                    cmd.Parameters.AddWithValue("@year", currentYear);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            decimal target = (decimal)reader["target_sales"];
                            decimal achieved = (decimal)reader["achieved_sales"];
                            decimal percent = target > 0 ? Math.Round((achieved / target) * 100, 1) : 0;

                            employees.Add(new
                            {
                                id = reader["user_id"],
                                name = reader["full_name"].ToString(),
                                position = reader["position"]?.ToString() ?? "Không rõ",
                                achieved,
                                target,
                                percent,
                                evaluation = reader["evaluation"].ToString()
                            });
                        }
                    }
                }
            }

            return Json(employees);
        }

        // Quản lý cập nhật KPI và nhận xét
        [HttpPost]
        public IActionResult UpdateKPI([FromBody] KPIUpdateModel model)
        {
            if (model == null || model.UserId <= 0)
                return BadRequest("Dữ liệu không hợp lệ.");

            var connStr = _config.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();

                //Tính lại doanh số đã đạt trong tháng đó
                decimal achieved = 0;
                string sumSql = @"
                        SELECT ISNULL(SUM(total_amount), 0)
                        FROM dbo.Invoices
                        WHERE seller_id = @uid 
                          AND status = 'completed'
                          AND MONTH(invoice_date) = @month 
                          AND YEAR(invoice_date) = @year";

                using (var sumCmd = new SqlCommand(sumSql, conn))
                {
                    sumCmd.Parameters.AddWithValue("@uid", model.UserId);
                    sumCmd.Parameters.AddWithValue("@month", model.Month);
                    sumCmd.Parameters.AddWithValue("@year", model.Year);
                    achieved = Convert.ToDecimal(sumCmd.ExecuteScalar());
                }

                // Cập nhật hoặc thêm KPI
                string checkSql = "SELECT COUNT(*) FROM dbo.KPI WHERE user_id = @uid AND [month] = @month AND [year] = @year";
                using (var checkCmd = new SqlCommand(checkSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("@uid", model.UserId);
                    checkCmd.Parameters.AddWithValue("@month", model.Month);
                    checkCmd.Parameters.AddWithValue("@year", model.Year);

                    int count = (int)checkCmd.ExecuteScalar();
                    if (count == 0)
                    {
                        // Insert 
                        string insertSql = @"
                    INSERT INTO dbo.KPI (user_id, [month], [year], target_sales, achieved_sales, evaluation)
                    VALUES (@uid, @month, @year, @target, @achieved, @evaluation)";
                        using (var cmd = new SqlCommand(insertSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@uid", model.UserId);
                            cmd.Parameters.AddWithValue("@month", model.Month);
                            cmd.Parameters.AddWithValue("@year", model.Year);
                            cmd.Parameters.AddWithValue("@target", model.TargetSales);
                            cmd.Parameters.AddWithValue("@achieved", achieved);
                            cmd.Parameters.AddWithValue("@evaluation", model.Evaluation ?? (object)DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        // Update
                        string updateSql = @"
                    UPDATE dbo.KPI
                    SET target_sales = @target,
                        achieved_sales = @achieved,
                        evaluation = @evaluation
                    WHERE user_id = @uid AND [month] = @month AND [year] = @year";
                        using (var cmd = new SqlCommand(updateSql, conn))
                        {
                            cmd.Parameters.AddWithValue("@uid", model.UserId);
                            cmd.Parameters.AddWithValue("@month", model.Month);
                            cmd.Parameters.AddWithValue("@year", model.Year);
                            cmd.Parameters.AddWithValue("@target", model.TargetSales);
                            cmd.Parameters.AddWithValue("@achieved", achieved);
                            cmd.Parameters.AddWithValue("@evaluation", model.Evaluation ?? (object)DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }

            return Json(new { success = true });
        }
    }
}
