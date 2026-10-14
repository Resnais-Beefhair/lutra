$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetRoot = Join-Path $PSScriptRoot '..\assets'
$images = @()
foreach ($size in @(16,32,48,64,256)) {
    $bitmap = [System.Drawing.Bitmap]::new($size,$size)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.ScaleTransform($size / 64.0, $size / 64.0)
    $bg = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#252e2c'))
    $g.FillRectangle($bg,0,0,64,64)
    foreach ($item in @(@('#99cdb7',16,12,40,26,50,53), @('#ffffff',32,12,29,38,35,53))) {
        $pen = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml($item[0]),7)
        $pen.StartCap = $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
        $path.AddLine([single]$item[1], [single]$item[2], [single]$item[1], [single]$item[3])
        $path.AddBezier([single]$item[1],[single]$item[3],[single]$item[1],[single]$item[4+1],[single]$item[4],[single]$item[5],[single]$item[4],[single]$item[5])
        $path.AddLine([single]$item[4],[single]$item[5],[single]$item[6],[single]$item[5])
        $g.DrawPath($pen,$path); $path.Dispose(); $pen.Dispose()
    }
    $memory = [System.IO.MemoryStream]::new()
    $bitmap.Save($memory,[System.Drawing.Imaging.ImageFormat]::Png)
    $images += ,$memory.ToArray()
    if ($size -eq 256) { $bitmap.Save((Join-Path $assetRoot 'lutra.png'),[System.Drawing.Imaging.ImageFormat]::Png) }
    $memory.Dispose(); $g.Dispose(); $bg.Dispose(); $bitmap.Dispose()
}
$stream = [System.IO.File]::Create((Join-Path $assetRoot 'lutra.ico'))
$writer = [System.IO.BinaryWriter]::new($stream)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
$dimensions = @(16,32,48,64,0)
for ($i=0; $i -lt $images.Count; $i++) {
    $writer.Write([byte]$dimensions[$i]); $writer.Write([byte]$dimensions[$i]); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($bytes in $images) { $writer.Write([byte[]]$bytes) }
$writer.Dispose(); $stream.Dispose()

