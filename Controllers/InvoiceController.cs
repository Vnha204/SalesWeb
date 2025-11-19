using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SalesWeb.Models;
using System.Data;

namespace SalesWeb.Controllers
{
    public class InvoiceController : Controller
    {
        private readonly IConfiguration _config;

        public InvoiceController(IConfiguration config)
        {
            _config = config;
        }

        public IActionResult Index()
        {
            return View();
        }

        //Lấy danh sách hóa đơn
        [HttpGet]
        public async Task<IActionResult> GetInvoices(string filter = "week", string search = "", int page = 1, int pageSize = 30)
        {
            var invoices = new List<object>();
            int totalCount = 0;
            decimal totalAmount = 0;

            using (var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                await conn.OpenAsync();

                string sqlBase = @"
                    FROM dbo.Invoices
                    WHERE status IN ('completed', 'returned')";

                if (filter == "today")
                    sqlBase += " AND CAST(invoice_date AS DATE) = CAST(GETDATE() AS DATE)";
                else if (filter == "week")
                    sqlBase += " AND invoice_date >= DATEADD(DAY, -7, GETDATE())";
                else if (filter == "month")
                    sqlBase += " AND MONTH(invoice_date) = MONTH(GETDATE()) AND YEAR(invoice_date) = YEAR(GETDATE())";
                else if (filter == "year")
                    sqlBase += " AND YEAR(invoice_date) = YEAR(GETDATE())";

                if (!string.IsNullOrEmpty(search))
                    sqlBase += " AND CAST(invoice_id AS NVARCHAR) LIKE @search";

                // Đếm số hóa đơn, tổng tiền
                string countSql = "SELECT COUNT(*), COALESCE(SUM(total_amount), 0) " + sqlBase;
                using (var countCmd = new SqlCommand(countSql, conn))
                {
                    if (!string.IsNullOrEmpty(search))
                        countCmd.Parameters.AddWithValue("@search", $"%{search}%");

                    using (var reader = await countCmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            totalCount = reader.GetInt32(0);
                            totalAmount = reader.GetDecimal(1);
                        }
                    }
                }

                // Lấy danh sách hóa đơn có phân trang
                int skip = (page - 1) * pageSize;
                string dataSql = $@"
                    SELECT invoice_id, invoice_date, total_amount, status, return_invoice_id
                    {sqlBase}
                    ORDER BY invoice_date DESC
                    OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;";

                using (var cmd = new SqlCommand(dataSql, conn))
                {
                    cmd.Parameters.AddWithValue("@skip", skip);
                    cmd.Parameters.AddWithValue("@take", pageSize);
                    if (!string.IsNullOrEmpty(search))
                        cmd.Parameters.AddWithValue("@search", $"%{search}%");

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            invoices.Add(new
                            {
                                id = reader.GetInt32(0),
                                date = reader.GetDateTime(1).ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                                total = reader.GetDecimal(2),
                                status = reader.GetString(3),
                                returnId = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4)
                            });
                        }
                    }
                }
            }

            return Json(new { invoices, totalCount, totalAmount });
        }

        // Lấy chi tiết hóa đơn
        [HttpGet]
        public async Task<IActionResult> GetInvoiceDetails(int invoiceId)
        {
            var details = new List<object>();

            using (var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                await conn.OpenAsync();

                string sql = @"
                    SELECT p.product_id, p.name, d.quantity, d.unit_price, (d.quantity * d.unit_price) AS total
                    FROM dbo.InvoiceDetails d
                    INNER JOIN dbo.Products p ON d.product_id = p.product_id
                    WHERE d.invoice_id = @id";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", invoiceId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            details.Add(new
                            {
                                productId = reader.GetInt32(0),
                                name = reader.GetString(1),
                                quantity = reader.GetInt32(2),
                                price = reader.GetDecimal(3),
                                total = reader.GetDecimal(4)
                            });
                        }
                    }
                }
            }

            return Json(details);
        }

        // Lấy chi tiết hóa đơn kèm số lượng đã trả
        [HttpGet]
        public async Task<IActionResult> GetInvoiceReturnDetails(int invoiceId)
        {
            var details = new List<object>();

            using (var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                await conn.OpenAsync();

                string sql = @"
                    SELECT 
                        p.product_id,
                        p.name,
                        ISNULL(d.quantity, 0) AS bought_qty,
                        ISNULL(d.unit_price, 0) AS unit_price,
                        ISNULL(r.returned_qty, 0) AS returned_qty
                    FROM dbo.InvoiceDetails d
                    INNER JOIN dbo.Products p ON d.product_id = p.product_id
                    LEFT JOIN (
                        SELECT 
                            ri.original_invoice_id, 
                            rid.product_id, 
                            SUM(rid.quantity) AS returned_qty
                        FROM dbo.ReturnInvoiceDetails rid
                        INNER JOIN dbo.ReturnInvoices ri 
                            ON rid.return_invoice_id = ri.return_invoice_id
                        GROUP BY ri.original_invoice_id, rid.product_id
                    ) r ON r.original_invoice_id = d.invoice_id AND r.product_id = d.product_id
                    WHERE d.invoice_id = @invoiceId
                    ORDER BY p.name;";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@invoiceId", invoiceId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int bought = reader.GetInt32(2);
                            int returned = reader.GetInt32(4);
                            decimal price = reader.GetDecimal(3);

                            details.Add(new
                            {
                                productId = reader.GetInt32(0),
                                name = reader.GetString(1),
                                boughtQty = bought,
                                unitPrice = price,
                                returnedQty = returned,
                                remainQty = Math.Max(0, bought - returned)
                            });
                        }
                    }
                }
            }

            return Json(details);
        }

        // Xử lý trả hàng (gọi sp_CreateReturnInvoice)
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ProcessReturn([FromBody] ReturnRequest request)
        {
            if (request == null || request.Details == null || !request.Details.Any())
                return Json(new { success = false, message = "Dữ liệu trả hàng không hợp lệ." });

            try
            {
                var dt = new DataTable();
                dt.Columns.Add("product_id", typeof(int));
                dt.Columns.Add("quantity", typeof(int));
                dt.Columns.Add("refund_price", typeof(decimal));

                foreach (var d in request.Details)
                    dt.Rows.Add(d.ProductId, d.Quantity, d.RefundPrice);

                int newReturnId = 0;

                using (var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    await conn.OpenAsync();

                    using (var cmd = new SqlCommand("dbo.sp_CreateReturnInvoice", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@original_invoice_id", request.OriginalInvoiceId);
                        cmd.Parameters.AddWithValue("@processed_by", request.ProcessedBy);
                        cmd.Parameters.AddWithValue("@reason", request.Reason ?? (object)DBNull.Value);

                        var tvp = cmd.Parameters.AddWithValue("@details", dt);
                        tvp.SqlDbType = SqlDbType.Structured;
                        tvp.TypeName = "dbo.ReturnDetailType";

                        var output = new SqlParameter("@new_return_id", SqlDbType.Int)
                        {
                            Direction = ParameterDirection.Output
                        };
                        cmd.Parameters.Add(output);

                        await cmd.ExecuteNonQueryAsync();
                        newReturnId = Convert.ToInt32(output.Value);
                    }
                }

                return Json(new { success = true, returnId = newReturnId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
