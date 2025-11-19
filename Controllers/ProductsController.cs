using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SalesWeb.Models;
using System.Data;

namespace SalesWeb.Controllers
{
    [Route("Products")]
    public class ProductsController : Controller
    {
        private readonly IConfiguration _config;

        public ProductsController(IConfiguration config)
        {
            _config = config;
        }

        public IActionResult Index()
        {
            ViewData["UserRole"] = HttpContext.Session.GetString("UserRole");
            return View();
        }

        //Lấy tất cả sản phẩm
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var list = new List<Product>();

            using (var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                await conn.OpenAsync();
                string sql = @"SELECT product_id, product_code, name, price, stock_quantity, image_url, description
                               FROM dbo.Products
                               WHERE is_active = 1
                               ORDER BY created_at DESC";

                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        list.Add(new Product
                        {
                            ProductId = reader.GetInt32(0),
                            ProductCode = reader.IsDBNull(1) ? null : reader.GetString(1),
                            Name = reader.GetString(2),
                            Price = reader.GetDecimal(3),
                            StockQuantity = reader.GetInt32(4),
                            ImageUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
                            Description = reader.IsDBNull(6) ? null : reader.GetString(6)
                        });
                    }
                }
            }

            return Json(list);
        }

        //Tìm kiếm theo mã hoặc tên
        [HttpGet("Search")]
        public async Task<IActionResult> Search(string keyword)
        {
            var list = new List<Product>();

            using (var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                await conn.OpenAsync();
                string sql = @"SELECT product_id, product_code, name, price, stock_quantity, image_url, description
                               FROM dbo.Products
                               WHERE product_code LIKE @kw OR name LIKE @kw
                               ORDER BY created_at DESC";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@kw", $"%{keyword}%");
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new Product
                            {
                                ProductId = reader.GetInt32(0),
                                ProductCode = reader.IsDBNull(1) ? null : reader.GetString(1),
                                Name = reader.GetString(2),
                                Price = reader.GetDecimal(3),
                                StockQuantity = reader.GetInt32(4),
                                ImageUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
                                Description = reader.IsDBNull(6) ? null : reader.GetString(6)
                            });
                        }
                    }
                }
            }

            return Json(list);
        }

        // Thêm sản phẩm (Manager Only)
        [HttpPost("Add")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Add(Product model, IFormFile imageFile)
        {
            if (HttpContext.Session.GetString("UserRole") != "manager")
                return Json(new { success = false, message = "Bạn không có quyền thêm sản phẩm." });

            if (string.IsNullOrEmpty(model.Name))
                return Json(new { success = false, message = "Tên sản phẩm không được để trống" });

            try
            {
                string? imagePath = null;

                if (imageFile != null && imageFile.Length > 0)
                {
                    var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "image");
                    if (!Directory.Exists(uploadDir))
                        Directory.CreateDirectory(uploadDir);

                    var fileName = Path.GetFileNameWithoutExtension(imageFile.FileName)
                                 + "_" + DateTime.Now.Ticks
                                 + Path.GetExtension(imageFile.FileName);
                    var filePath = Path.Combine(uploadDir, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }

                    imagePath = "image/" + fileName;
                }

                using (var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    await conn.OpenAsync();

                    string newCode = "SP" + DateTime.Now.Ticks.ToString().Substring(10);

                    string sql = @"
                INSERT INTO dbo.Products (product_code, name, price, stock_quantity, image_url, description, is_active, created_at)
                VALUES (@code, @name, @price, @stock, @img, @desc, 1, GETDATE());
                SELECT SCOPE_IDENTITY();";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@code", newCode);
                        cmd.Parameters.AddWithValue("@name", model.Name);
                        cmd.Parameters.AddWithValue("@price", model.Price);
                        cmd.Parameters.AddWithValue("@stock", model.StockQuantity);
                        cmd.Parameters.AddWithValue("@img", (object?)imagePath ?? "https://via.placeholder.com/240x160?text=Ảnh");
                        cmd.Parameters.AddWithValue("@desc", (object?)model.Description ?? DBNull.Value);

                        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        return Json(new { success = true, id, code = newCode, img = imagePath });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi thêm sản phẩm: " + ex.Message });
            }
        }


        //Cập nhật sản phẩm (Manager Only)
        [HttpPost("Update")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Update([FromBody] Product model)
        {
            if (HttpContext.Session.GetString("UserRole") != "manager")
                return Json(new { success = false, message = "Bạn không có quyền chỉnh sửa sản phẩm." });

            if (model.ProductId <= 0)
                return Json(new { success = false, message = "ID không hợp lệ" });
            // Chuẩn hóa lại đường dẫn ảnh
            if (!string.IsNullOrEmpty(model.ImageUrl))
            {
              
                var uriPrefix = $"{Request.Scheme}://{Request.Host}/";
                if (model.ImageUrl.StartsWith(uriPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    model.ImageUrl = model.ImageUrl.Replace(uriPrefix, "");
                }
                if (model.ImageUrl.StartsWith("/"))
                {
                    model.ImageUrl = model.ImageUrl.TrimStart('/');
                }
            }

            using (var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                await conn.OpenAsync();

                string sql = @"
                    UPDATE dbo.Products
                    SET name=@name, price=@price, stock_quantity=@stock, 
                        image_url=@img, description=@desc
                    WHERE product_id=@id";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@name", model.Name);
                    cmd.Parameters.AddWithValue("@price", model.Price);
                    cmd.Parameters.AddWithValue("@stock", model.StockQuantity);
                    cmd.Parameters.AddWithValue("@img", (object?)model.ImageUrl ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@desc", (object?)model.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@id", model.ProductId);

                    int rows = await cmd.ExecuteNonQueryAsync();
                    return Json(new { success = rows > 0 });
                }
            }
        }

        //Xóa sản phẩm (Manager Only - Soft Delete)
        [HttpPost("Delete")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Delete([FromBody] int id)
        {
            if (HttpContext.Session.GetString("UserRole") != "manager")
                return Json(new { success = false, message = "Bạn không có quyền xóa sản phẩm." });

            if (id <= 0)
                return Json(new { success = false, message = "ID không hợp lệ." });

            using (var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                await conn.OpenAsync();
                string sql = "UPDATE dbo.Products SET is_active = 0 WHERE product_id = @id";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    int rows = await cmd.ExecuteNonQueryAsync();
                    return Json(new
                    {
                        success = rows > 0,
                        message = rows > 0 ? "Đã ẩn sản phẩm." : "Không tìm thấy sản phẩm để ẩn."
                    });
                }
            }
        }
    }
}
