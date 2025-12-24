using System.Text;

namespace Dataportal.Services.Email;

public class ThemedEmailTemplateRenderer : IEmailTemplateRenderer
{
    public string RenderHtml(string subject, string bodyHtml)
    {
        var builder = new StringBuilder();

        builder.Append("""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta http-equiv="X-UA-Compatible" content="IE=edge">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>
""");
        builder.Append(subject);
        builder.Append("""
  </title>
  <style>
    :root { color-scheme: light only; }
    body {
      margin: 0;
      font-family: "Segoe UI", -apple-system, BlinkMacSystemFont, "Helvetica Neue", Arial, sans-serif;
      background: transparent;
      color: #0f172a;
      padding: 0;
    }
    .wrapper {
      width: 100%;
      padding: 32px 12px 16px;
      background: transparent;
    }
    .card {
      max-width: 640px;
      margin: 0 auto;
      background: #ffffff;
      border-radius: 18px;
      border: 1px solid #c8102e;
      box-shadow: 0 22px 38px -16px rgba(0, 0, 0, 0.18);
      overflow: hidden;
    }
    .header {
      padding: 22px 24px 16px;
      border-bottom: 1px solid rgba(200, 16, 46, 0.16);
      background: #ffffff;
    }
    .brand {
      display: inline-flex;
      align-items: center;
      gap: 10px;
      font-weight: 700;
      font-size: 18px;
      letter-spacing: -0.01em;
      color: #c8102e;
      text-transform: uppercase;
    }
    .brand-badge {
      width: 10px;
      height: 10px;
      border-radius: 50%;
      background: #c8102e;
    }
    .title {
      font-size: 24px;
      margin: 12px 0 0 0;
      color: #0f172a;
      letter-spacing: -0.01em;
    }
    .body {
      padding: 24px;
      line-height: 1.6;
      color: #1f2937;
      background: #ffffff;
    }
    .footer {
      padding: 18px 24px 24px;
      font-size: 13px;
      color: #4b5563;
      background: #ffffff;
      border-top: 1px solid rgba(200, 16, 46, 0.12);
    }
    a {
      color: #c8102e;
      text-decoration: none;
      font-weight: 600;
    }
    .pill {
      display: inline-flex;
      align-items: center;
      padding: 6px 12px;
      border-radius: 9999px;
      background: rgba(200, 16, 46, 0.08);
      color: #c8102e;
      font-size: 12px;
      letter-spacing: 0.01em;
      text-transform: uppercase;
      margin-bottom: 10px;
    }
  </style>
</head>
<body>
  <div class="wrapper">
    <div class="card">
      <div class="header">
        <div class="brand"><span class="brand-badge"></span><span>Dataportal</span></div>
        <div class="pill">Notification</div>
        <h1 class="title">
""");
        builder.Append(subject);
        builder.Append("""
        </h1>
      </div>
      <div class="body">
""");
        builder.Append(bodyHtml);
        builder.Append("""
      </div>
        <div class="footer">
          You are receiving this message because you have a relationship with the Dataportal platform. If you believe this is an error, please let us know.
          This mailbox is not monitored and cannot receive replies.
        </div>
    </div>
  </div>
</body>
</html>
""");

        return builder.ToString();
    }
}