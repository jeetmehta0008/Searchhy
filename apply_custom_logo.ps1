Add-Type -AssemblyName System.Drawing

$uploadDir = "C:\Users\Jeet Mehta\.gemini\antigravity-ide\brain\34c30760-8f9b-469a-98ae-3b110dbb5764\.user_uploaded"
$latestFile = (Get-ChildItem -Path $uploadDir | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName

Write-Output "Using uploaded image: $latestFile"

$projDir = "c:\Users\Jeet Mehta\Desktop\Pojects\Searchhy"
$destPng = Join-Path $projDir "logo.png"
$destIco = Join-Path $projDir "app.ico"

# 1. Copy to logo.png
Copy-Item $latestFile $destPng -Force

# 2. Build multi-resolution ICO file (256, 128, 64, 48, 32, 16)
$original = [System.Drawing.Image]::FromFile($destPng)
$sizes = @(256, 128, 64, 48, 32, 16)

$fs = New-Object System.IO.FileStream($destIco, [System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter($fs)

# ICO Header
$bw.Write([int16]0)
$bw.Write([int16]1)
$bw.Write([int16]$sizes.Length)

$imageStreams = @()
$offset = 6 + (16 * $sizes.Length)

foreach ($size in $sizes) {
    $resized = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($resized)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($original, 0, 0, $size, $size)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $resized.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $resized.Dispose()

    $imageStreams += $ms

    $w = if ($size -eq 256) { [byte]0 } else { [byte]$size }
    $h = if ($size -eq 256) { [byte]0 } else { [byte]$size }
    $bw.Write($w)
    $bw.Write($h)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([int16]1)
    $bw.Write([int16]32)
    $bw.Write([int]$ms.Length)
    $bw.Write([int]$offset)

    $offset += $ms.Length
}

foreach ($ms in $imageStreams) {
    $bytes = $ms.ToArray()
    $bw.Write($bytes)
    $ms.Dispose()
}

$bw.Flush()
$fs.Flush()
$fs.Close()
$original.Dispose()

# 3. Copy to build output folder
$binDir = Join-Path $projDir "bin\Release\net8.0-windows"
if (Test-Path $binDir) {
    Copy-Item $destIco (Join-Path $binDir "app.ico") -Force
    Copy-Item $destPng (Join-Path $binDir "logo.png") -Force
}

# 4. Re-create Desktop Shortcut
$desktopPath = [System.Environment]::GetFolderPath("Desktop")
$lnkPath = Join-Path $desktopPath "Searchhy.lnk"
if (Test-Path $lnkPath) { Remove-Item $lnkPath -Force }

$wsh = New-Object -ComObject WScript.Shell
$shortcut = $wsh.CreateShortcut($lnkPath)
$shortcut.TargetPath = Join-Path $binDir "Searchhy.exe"
$shortcut.WorkingDirectory = $binDir
$shortcut.IconLocation = "$destIco,0"
$shortcut.Description = "Searchhy - Google Circle to Search for Windows"
$shortcut.Save()

# 5. Flush Windows Shell Icon Cache
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class ShellRefresh {
    [DllImport("shell32.dll")]
    public static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    public static void Flush() {
        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }
}
"@

[ShellRefresh]::Flush()

Write-Output "SUCCESS: True multi-res ICO created and Windows desktop icon refreshed."
