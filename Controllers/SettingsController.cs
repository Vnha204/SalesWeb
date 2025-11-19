using Microsoft.AspNetCore.Mvc;
using SalesWeb.Data;
using SalesWeb.Models;
using System.Linq;

public class SettingsController : Controller
{
    private readonly AppDbContext _context;

    public SettingsController(AppDbContext context)
    {
        _context = context;
    }

    public IActionResult Settings()
    {
        var setting = _context.AppSettings
            .FirstOrDefault(s => s.SettingKey == "EnablePrintPreview");

        bool enabled = setting?.SettingValue == "true";

        ViewData["EnablePrintPreview"] = enabled;

        return View();
    }

    [HttpPost]
    public IActionResult UpdatePrintPreview(bool enabled)
    {
        var setting = _context.AppSettings
            .FirstOrDefault(s => s.SettingKey == "EnablePrintPreview");

        if (setting == null)
        {
            setting = new AppSetting
            {
                SettingKey = "EnablePrintPreview"
            };
            _context.AppSettings.Add(setting);
        }

        setting.SettingValue = enabled ? "true" : "false";
        _context.SaveChanges();

        TempData["msg"] = "Đã cập nhật cài đặt!";
        return RedirectToAction("Settings");
    }

    [HttpGet]
    public bool GetPrintPreviewStatus()
    {
        var setting = _context.AppSettings
            .FirstOrDefault(s => s.SettingKey == "EnablePrintPreview");

        return setting?.SettingValue == "true";
    }
}
