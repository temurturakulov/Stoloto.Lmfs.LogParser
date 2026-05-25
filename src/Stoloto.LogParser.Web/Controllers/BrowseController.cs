using System.Windows.Forms;
using Microsoft.AspNetCore.Mvc;

namespace Stoloto.LogParser.Web.Controllers;

[ApiController]
[Route("api/browse")]
public class BrowseController : ControllerBase
{
    [HttpGet]
    public IActionResult Browse([FromQuery] bool isFile = false)
    {
        string? selected = null;

        var thread = new Thread(() =>
        {
            using var owner = new Form { TopMost = true, ShowInTaskbar = false, WindowState = FormWindowState.Minimized };
            owner.Show();

            if (isFile)
            {
                using var dlg = new OpenFileDialog
                {
                    Title  = "Выберите файл лога",
                    Filter = "Log files (*.log)|*.log|All files (*.*)|*.*"
                };
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                    selected = dlg.FileName;
            }
            else
            {
                using var dlg = new FolderBrowserDialog
                {
                    Description            = "Выберите папку с логами",
                    UseDescriptionForTitle = true
                };
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                    selected = dlg.SelectedPath;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        return Ok(new { path = selected });
    }
}
