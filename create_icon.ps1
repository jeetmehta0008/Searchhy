Add-Type -AssemblyName System.Drawing

$uploadDir = "C:\Users\Jeet Mehta\.gemini\antigravity-ide\brain\34c30760-8f9b-469a-98ae-3b110dbb5764\.user_uploaded"
$latestFile = (Get-ChildItem -Path $uploadDir | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName

Write-Output "Found latest uploaded file: $latestFile"

$destPng = "c:\Users\Jeet Mehta\Desktop\Pojects\Searchhy\logo.png"
$destIco = "c:\Users\Jeet Mehta\Desktop\Pojects\Searchhy\app.ico"

# 1. Copy as PNG
Copy-Item $latestFile $destPng -Force

# 2. Generate multi-resolution ICO (256x256)
$bmp = [System.Drawing.Image]::FromFile($destPng)
$resized = New-Object System.Drawing.Bitmap(256, 256)
$g = [System.Drawing.Graphics]::FromImage($resized)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.DrawImage($bmp, 0, 0, 256, 256)
$g.Dispose()

$hIcon = $resized.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)
$fs = New-Object System.IO.FileStream($destIco, [System.IO.FileMode]::Create)
$icon.Save($fs)
$fs.Close()
$resized.Dispose()
$bmp.Dispose()

# 3. Update Desktop Shortcut pointing permanently to project folder
$wsh = New-Object -ComObject WScript.Shell
$shortcut = $wsh.CreateShortcut("C:\Users\Jeet Mehta\Desktop\Searchhy.lnk")
$shortcut.TargetPath = "c:\Users\Jeet Mehta\Desktop\Pojects\Searchhy\bin\Release\net8.0-windows\Searchhy.exe"
$shortcut.WorkingDirectory = "c:\Users\Jeet Mehta\Desktop\Pojects\Searchhy\bin\Release\net8.0-windows"
$shortcut.IconLocation = "c:\Users\Jeet Mehta\Desktop\Pojects\Searchhy\app.ico,0"
$shortcut.Description = "Searchhy - Google Circle to Search for Windows"
$shortcut.Save()

Write-Output "SUCCESS: Custom user logo applied to app.ico, logo.png, and Desktop Shortcut."
