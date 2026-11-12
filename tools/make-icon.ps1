# Génère src/BetterFences/Assets/BetterFences.ico (16 à 256 px) : carré arrondi bleu + grille 2×2 de tuiles.
param([string]$Out = (Join-Path $PSScriptRoot '..\src\BetterFences\Assets\BetterFences.ico'))
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

function RoundRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
  $d = $r * 2
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddArc($x, $y, $d, $d, 180, 90)
  $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
  $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
  $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
  $p.CloseFigure()
  return $p
}

function Frame([int]$s) {
  $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'
  $u = $s / 32.0

  # Fond : dégradé bleu, coins arrondis
  $m = [Math]::Max(0.5, $u * 1)
  $bg = RoundRect $m $m ($s - 2 * $m) ($s - 2 * $m) (9 * $u)
  $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF $s, $s), ([System.Drawing.Color]::FromArgb(255, 0x6A, 0x93, 0xFF)), ([System.Drawing.Color]::FromArgb(255, 0x3B, 0x5B, 0xDB))
  $g.FillPath($grad, $bg)

  # Tuiles 2×2 (une teintée pour rappeler les icônes d'applis)
  $cell = 7.2 * $u; $gap = 2.4 * $u; $start = ($s - (2 * $cell + $gap)) / 2
  $colors = @(
    [System.Drawing.Color]::White,
    [System.Drawing.Color]::FromArgb(255, 0xFF, 0xD1, 0x66),
    [System.Drawing.Color]::FromArgb(255, 0xE6, 0xEC, 0xFF),
    [System.Drawing.Color]::White)
  $k = 0
  foreach ($row in 0, 1) { foreach ($col in 0, 1) {
    $x = $start + $col * ($cell + $gap); $y = $start + $row * ($cell + $gap)
    if ($s -ge 48) {
      $sh = RoundRect $x ($y + 0.8 * $u) $cell $cell (2.4 * $u)
      $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(50, 0, 0, 40))), $sh)
    }
    $p = RoundRect $x $y $cell $cell (2.4 * $u)
    $g.FillPath((New-Object System.Drawing.SolidBrush $colors[$k]), $p); $k++
  } }
  $g.Dispose()
  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  return , $ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$frames = $sizes | ForEach-Object { , (Frame $_) }
New-Item -ItemType Directory -Force -Path (Split-Path $Out) | Out-Null
$fs = [System.IO.File]::Create($Out); $w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $sz = $sizes[$i] % 256
  $w.Write([byte]$sz); $w.Write([byte]$sz); $w.Write([byte]0); $w.Write([byte]0)
  $w.Write([uint16]1); $w.Write([uint16]32)
  $w.Write([uint32]$frames[$i].Length); $w.Write([uint32]$offset)
  $offset += $frames[$i].Length
}
foreach ($f in $frames) { $w.Write($f) }
$w.Close()
"Icône écrite : $Out"
# Aperçu 256 px
[System.IO.File]::WriteAllBytes([System.IO.Path]::ChangeExtension($Out, '.png'), $frames[-1])
