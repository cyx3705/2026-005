$ErrorActionPreference = 'Stop'
$inputPath = [System.IO.Path]::GetFullPath($args[0])
$outputPath = [System.IO.Path]::GetFullPath($args[1])
$word = $null
$doc = $null
try {
    $word = New-Object -ComObject Word.Application
    $word.Visible = $false
    $word.DisplayAlerts = 0
    $doc = $word.Documents.Open($inputPath, $false, $true)
    $doc.ExportAsFixedFormat($outputPath, 17, $false, 0, 0, 1, 1, 0, $true, $false, 0, $true, $true, $false)
}
finally {
    if ($doc -ne $null) { $doc.Close($false) }
    if ($word -ne $null) { $word.Quit($false) }
    [System.GC]::Collect()
    [System.GC]::WaitForPendingFinalizers()
}
Write-Output $outputPath
