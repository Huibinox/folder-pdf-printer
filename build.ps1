$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
New-Item -ItemType Directory -Force build,dist/FolderPdfPrinter | Out-Null
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /out:dist\FolderPdfPrinter\FolderPdfPrinter.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll src\Core.cs src\Program.cs
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
& $compiler /nologo /target:exe /platform:x64 /out:build\Tests.exe src\Core.cs tests\Tests.cs
if ($LASTEXITCODE -ne 0) { throw 'Test build failed' }
& ./build/Tests.exe
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
Copy-Item README.md dist/FolderPdfPrinter/README.md -Force
Copy-Item LICENSE dist/FolderPdfPrinter/LICENSE -Force
Write-Host 'Build and tests passed.'
