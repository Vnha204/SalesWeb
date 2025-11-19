using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SalesWeb.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SalesWeb.Controllers
{
    public class ReportController : Controller
    {
        private readonly IConfiguration _config;

        public ReportController(IConfiguration config)
        {
            _config = config;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public JsonResult GetReportData(int? month = null)
        {
            var connStr = _config.GetConnectionString("DefaultConnection");
            var reportData = new ReportData();

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();

                using (var cmd = new SqlCommand(@"
            SELECT TOP 7 
                CONVERT(date, i.invoice_date) AS sale_date,
                SUM(d.quantity * d.unit_price) AS total_sales,
                SUM(CASE 
                        WHEN p.category = N'Máy tính' THEN d.quantity * d.unit_price * 0.05
                        ELSE d.quantity * d.unit_price * 0.10
                    END) AS total_profit
            FROM dbo.InvoiceDetails d
            JOIN dbo.Invoices i ON d.invoice_id = i.invoice_id
            JOIN dbo.Products p ON d.product_id = p.product_id
            WHERE i.status = 'completed'
            GROUP BY CONVERT(date, i.invoice_date)
            ORDER BY sale_date DESC", conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        var tmp = new List<(DateTime date, decimal revenue, decimal profit, decimal cost)>();
                        while (reader.Read())
                        {
                            var date = (DateTime)reader["sale_date"];
                            var revenue = (decimal)reader["total_sales"];
                            var profit = (decimal)reader["total_profit"];
                            var cost = revenue - profit;

                            tmp.Add((date, revenue, profit, cost));

                            reportData.TotalRevenue += revenue;
                            reportData.TotalProfit += profit;
                            reportData.TotalCost += cost;
                        }

                        foreach (var d in tmp.OrderBy(x => x.date))
                        {
                            reportData.Labels.Add(d.date.ToString("dd/MM"));
                            reportData.Revenue.Add(d.revenue);
                            reportData.Profit.Add(d.profit);
                            reportData.Cost.Add(d.cost);
                        }
                    }
                }

                int monthFilter = month ?? DateTime.Now.Month;

                using (var cmd = new SqlCommand(@"
                    SELECT u.full_name, SUM(i.total_amount) AS total_sales
                    FROM dbo.Invoices i
                    JOIN dbo.Users u ON i.seller_id = u.user_id
                    WHERE i.status = 'completed'
                      AND MONTH(i.invoice_date) = @month
                      AND YEAR(i.invoice_date) = YEAR(GETDATE())
                    GROUP BY u.full_name
                    ORDER BY total_sales DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@month", monthFilter);

                    using (var reader = cmd.ExecuteReader())
                    {
                        var employees = new List<(string name, decimal total)>();
                        while (reader.Read())
                        {
                            employees.Add((reader["full_name"].ToString(), (decimal)reader["total_sales"]));
                        }

                        if (employees.Any())
                        {
                            decimal totalCompanySales = employees.Sum(e => e.total);
                            foreach (var e in employees)
                            {
                                var percent = totalCompanySales > 0
                                    ? Math.Round((e.total / totalCompanySales) * 100, 1)
                                    : 0;

                                reportData.Employees.Add(new
                                {
                                    name = e.name,
                                    percent
                                });
                            }
                        }
                    }
                }
            }

            return Json(reportData);
        }
    }
}
