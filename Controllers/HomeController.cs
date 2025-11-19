using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SalesWeb.Data;
using SalesWeb.Models;
using System.Data;
using System.Diagnostics;

namespace SalesWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public HomeController(ILogger<HomeController> logger, AppDbContext context, IConfiguration config)
        {
            _logger = logger;
            _context = context;
            _config = config;
        }

        [HttpPost]
        [IgnoreAntiforgeryToken] 
        public async Task<IActionResult> CreateInvoice([FromBody] InvoiceRequest request)
        {
            if (request == null || request.Details == null || !request.Details.Any())
                return Json(new { success = false, message = "Dữ liệu hóa đơn trống" });

            try
            {
                var dt = new DataTable();
                dt.Columns.Add("product_id", typeof(int));
                dt.Columns.Add("quantity", typeof(int));
                dt.Columns.Add("unit_price", typeof(decimal));

                foreach (var d in request.Details)
                    dt.Rows.Add(d.ProductId, d.Quantity, d.UnitPrice);

                int newInvoiceId = 0;

                using (var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    await conn.OpenAsync();

                    using (var cmd = new SqlCommand("dbo.sp_CreateInvoice", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@seller_id", request.SellerId);

                        var tvpParam = cmd.Parameters.AddWithValue("@details", dt);
                        tvpParam.SqlDbType = SqlDbType.Structured;
                        tvpParam.TypeName = "dbo.InvoiceDetailType";

                        var outputParam = new SqlParameter("@new_invoice_id", SqlDbType.Int)
                        {
                            Direction = ParameterDirection.Output
                        };
                        cmd.Parameters.Add(outputParam);

                        await cmd.ExecuteNonQueryAsync();

                        if (outputParam.Value != DBNull.Value)
                            newInvoiceId = Convert.ToInt32(outputParam.Value);
                    }
                }

                if (newInvoiceId > 0)
                {
                    return Json(new { success = true, invoiceId = newInvoiceId });
                }
                else
                {
                    return Json(new { success = false, message = "Không tạo được hóa đơn" });
                }
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(sqlEx, "SQL Error khi tạo hóa đơn");
                return Json(new { success = false, message = "Lỗi SQL: " + sqlEx.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo hóa đơn");
                return Json(new { success = false, message = ex.Message });
            }
        }
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SaveTempInvoice([FromBody] InvoiceRequest request)
        {
            if (request == null || request.Details == null || !request.Details.Any())
                return Json(new { success = false, message = "Dữ liệu hóa đơn trống" });

            try
            {
                var dt = new DataTable();
                dt.Columns.Add("product_id", typeof(int));
                dt.Columns.Add("quantity", typeof(int));
                dt.Columns.Add("unit_price", typeof(decimal));

                foreach (var d in request.Details)
                    dt.Rows.Add(d.ProductId, d.Quantity, d.UnitPrice);

                int newInvoiceId = 0;

                using (var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    await conn.OpenAsync();

                    using (var cmd = new SqlCommand("dbo.sp_SaveDraftInvoice", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@seller_id", request.SellerId);

                        var tvpParam = cmd.Parameters.AddWithValue("@details", dt);
                        tvpParam.SqlDbType = SqlDbType.Structured;
                        tvpParam.TypeName = "dbo.InvoiceDetailType";

                        var outputParam = new SqlParameter("@new_invoice_id", SqlDbType.Int)
                        {
                            Direction = ParameterDirection.Output
                        };
                        cmd.Parameters.Add(outputParam);

                        await cmd.ExecuteNonQueryAsync();
                        newInvoiceId = Convert.ToInt32(outputParam.Value);
                    }
                }

                return Json(new { success = true, invoiceId = newInvoiceId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu hóa đơn tạm");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSavedInvoices(int sellerId)
        {
            var list = new List<object>();
            using (var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(
                    "SELECT invoice_id, invoice_date, total_amount FROM dbo.Invoices WHERE seller_id=@sellerId AND status='draft' ORDER BY invoice_id DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@sellerId", sellerId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new
                            {
                                id = reader.GetInt32(0),
                                date = reader.GetDateTime(1).ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                                total = reader.GetDecimal(2)
                            });
                        }
                    }
                }
            }
            return Json(list);
        }
        [HttpGet]
        public async Task<IActionResult> GetInvoiceDetails(int invoiceId)
        {
            var list = new List<object>();
            using (var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(
                    "SELECT product_id, quantity, unit_price FROM dbo.InvoiceDetails WHERE invoice_id=@invoiceId", conn))
                {
                    cmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new
                            {
                                productId = reader.GetInt32(0),
                                quantity = reader.GetInt32(1),
                                unitPrice = reader.GetDecimal(2)
                            });
                        }
                    }
                }
            }
            return Json(list);
        }
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> PayTempInvoice([FromBody] InvoiceRequest request)
        {
            if (request == null || request.Details == null || !request.Details.Any())
                return Json(new { success = false, message = "Dữ liệu hóa đơn trống" });

            try
            {
                using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                int invoiceId = request.InvoiceId; 
                decimal total = request.Details.Sum(d => d.Quantity * d.UnitPrice);

                using (var tran = conn.BeginTransaction())
                {
                    // Cập nhật hóa đơn thành completed
                    var updateCmd = new SqlCommand(
                        "UPDATE dbo.Invoices SET status='completed', total_amount=@total WHERE invoice_id=@invoiceId",
                        conn, tran);
                    updateCmd.Parameters.AddWithValue("@total", total);
                    updateCmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                    await updateCmd.ExecuteNonQueryAsync();

                    var delCmd = new SqlCommand("DELETE FROM dbo.InvoiceDetails WHERE invoice_id=@invoiceId", conn, tran);
                    delCmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                    await delCmd.ExecuteNonQueryAsync();

                    foreach (var d in request.Details)
                    {
                        var insCmd = new SqlCommand(@"
                    INSERT INTO dbo.InvoiceDetails(invoice_id, product_id, quantity, unit_price)
                    VALUES(@invoiceId, @productId, @qty, @price)", conn, tran);
                        insCmd.Parameters.AddWithValue("@invoiceId", invoiceId);
                        insCmd.Parameters.AddWithValue("@productId", d.ProductId);
                        insCmd.Parameters.AddWithValue("@qty", d.Quantity);
                        insCmd.Parameters.AddWithValue("@price", d.UnitPrice);
                        await insCmd.ExecuteNonQueryAsync();
                    }

                    tran.Commit();
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi thanh toán hóa đơn tạm");
                return Json(new { success = false, message = ex.Message });
            }
        }
        public IActionResult Index()
        {
            var products = _context.Products.ToList();

            // Lấy ID người dùng từ session
            int? currentUserId = HttpContext.Session.GetInt32("CurrentUserId");
            ViewData["CurrentUserId"] = currentUserId ?? 0;

            return View(products);
        }
        [HttpGet]
        public async Task<IActionResult> SearchProducts(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                var all = await _context.Products
                    .Where(p => p.IsActive == true)
                    .Select(p => new
                    {
                        p.ProductId,
                        p.ProductCode,
                        p.Name,
                        p.Price,
                        p.ImageUrl
                    })
                    .ToListAsync();

                return Json(all);
            }

            var result = await _context.Products
                .Where(p => p.IsActive == true &&
                            (p.Name.Contains(keyword) || p.ProductCode.Contains(keyword)))
                .Select(p => new
                {
                    p.ProductId,
                    p.ProductCode,
                    p.Name,
                    p.Price,
                    p.ImageUrl
                })
                .ToListAsync();

            return Json(result);
        }



        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
