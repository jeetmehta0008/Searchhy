$projDir = "c:\Users\Jeet Mehta\Desktop\Pojects\Searchhy"
$pubDir = Join-Path $projDir "publish"

Copy-Item (Join-Path $projDir "app.ico") (Join-Path $pubDir "app.ico") -Force
Copy-Item (Join-Path $projDir "logo.png") (Join-Path $pubDir "logo.png") -Force

$zipPath = Join-Path $projDir "Searchhy-v1.0-Windows-x64.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Compress-Archive -Path (Join-Path $pubDir "*") -DestinationPath $zipPath -Force
Write-Output "SUCCESS: Package created at $zipPath"
