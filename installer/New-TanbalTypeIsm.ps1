<#
.SYNOPSIS
    Generates the InstallShield project (TanbalType.ism) for TanbalType.

.DESCRIPTION
    Builds a Basic MSI project from InstallShield's blank template using the
    InstallShield automation interface, then saves it in XML format so the
    project file is readable and diffable in git.

    The automation interface (ISWiAuto31) is a 32-bit in-process COM server, so
    this script MUST run under 32-bit PowerShell. build-installer.bat does that
    for you; to run it by hand:

        C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe ^
            -ExecutionPolicy Bypass -File installer\New-TanbalTypeIsm.ps1

    You only need to re-run this when the installer's structure changes. The
    generated TanbalType.ism is committed, so day-to-day you can just open it in
    InstallShield and edit it there.
#>
[CmdletBinding()]
param(
    # Where the generated project is written. Defaults to installer\TanbalType.ism.
    [string] $IsmPath,

    # Folder holding the published application files (see build-installer.bat).
    [string] $PayloadDir,

    # Product version; keep in sync with <Version> in TanbalType.csproj.
    [string] $ProductVersion = '1.5.0',

    # By default the .NET 8 Desktop Runtime is downloaded from Microsoft only if
    # the target machine does not already have it, which keeps setup.exe at a
    # few megabytes. Pass -BundleDotNet to pack the runtime into setup.exe
    # instead, for an installer that works with no internet connection at all.
    # That needs the redistributable in InstallShield's prerequisites folder;
    # build-installer.bat passes -prqdownload to fetch it once.
    [switch] $BundleDotNet,

    [string] $InstallShieldRoot = 'C:\Program Files (x86)\InstallShield\2025'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# $PSScriptRoot is not reliably populated while param() defaults are evaluated
# under Windows PowerShell 5.1, so the paths are resolved here instead.
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $IsmPath)    { $IsmPath    = Join-Path $scriptDir 'TanbalType.ism' }
if (-not $PayloadDir) { $PayloadDir = Join-Path $scriptDir 'payload' }

if ([IntPtr]::Size -ne 4) {
    throw 'This script needs 32-bit PowerShell (C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe) because the InstallShield automation interface is a 32-bit COM server.'
}

# The upgrade code identifies "TanbalType" across all versions. It must never
# change, or a new version will install alongside the old one instead of
# replacing it. The product code is regenerated per version, which is what
# Windows Installer expects for a major upgrade.
$UpgradeCode = '{4C1F9E27-4A4D-4C8E-9D3E-1B7A6F2C5E80}'

$template = Join-Path $InstallShieldRoot 'Support\0409\IsProjBlankTpl.ism'
if (-not (Test-Path $template)) { throw "InstallShield blank Basic MSI template not found: $template" }
if (-not (Test-Path $PayloadDir)) { throw "Payload folder not found: $PayloadDir`nRun build-installer.bat, or publish the app there first." }

# The four files dotnet publish produces for a framework-dependent build. The
# .pdb is deliberately left out: it is a debug symbol file, not something an
# end user needs.
$payloadFiles = @('TanbalType.exe', 'TanbalType.dll', 'TanbalType.deps.json', 'TanbalType.runtimeconfig.json') |
    ForEach-Object {
        $f = Join-Path $PayloadDir $_
        if (-not (Test-Path $f)) { throw "Missing payload file: $f" }
        (Get-Item $f).FullName
    }

$appIcon = (Get-Item (Join-Path $scriptDir '..\TanbalType\app.ico')).FullName

Write-Host "Creating $IsmPath from the blank Basic MSI template..."
if (Test-Path $IsmPath) { Remove-Item $IsmPath -Force }
Copy-Item $template $IsmPath -Force
Set-ItemProperty $IsmPath -Name IsReadOnly -Value $false

$project = New-Object -ComObject ISWiAuto31.ISWiProject
$project.OpenProject($IsmPath, $false)

try {
    # ---------- product information ----------
    $project.ProductName     = 'TanbalType'
    $project.ProductVersion  = $ProductVersion
    $project.ProductCode     = $project.GenerateGUID()
    $project.UpgradeCode     = $UpgradeCode
    $project.CompanyName     = 'Soroush Sarmast'
    $project.CompanyURL      = 'https://github.com/soroush75/TanbalType'
    $project.INSTALLDIR      = '[ProgramFilesFolder]TanbalType'

    # Software identification tags (ISO/IEC 19770-2) need a registered tag
    # creator id; without one every build emits two warnings, so turn them off.
    $project.EnableSwidtag = $false

    # ---------- properties ----------
    # Per-machine install; the app's own "اجرا در استارت‌آپ" menu item handles
    # auto-start per user, so the installer deliberately does not touch Run keys.
    $props = @{
        'ALLUSERS'        = '1'
        'ARPURLINFOABOUT' = 'https://github.com/soroush75/TanbalType'
        'ARPCONTACT'      = 'Soroush Sarmast'
        'ARPNOMODIFY'     = '1'
    }
    foreach ($name in $props.Keys) {
        $property = $project.ISWIProperties | Where-Object { $_.Name -eq $name }
        if (-not $property) { $property = $project.AddProperty($name) }
        $property.Value = $props[$name]
    }

    # The major-upgrade row added below uses PREVIOUSVERSIONSINSTALLED as its
    # action property, and Windows Installer only honours a public property
    # across a privileged install if it is declared secure.
    $secure = $project.ISWIProperties | Where-Object { $_.Name -eq 'SecureCustomProperties' }
    if (-not $secure) { $secure = $project.AddProperty('SecureCustomProperties') }
    $secureNames = @($secure.Value -split ';' | Where-Object { $_ })
    if ($secureNames -notcontains 'PREVIOUSVERSIONSINSTALLED') {
        $secure.Value = (@($secureNames) + 'PREVIOUSVERSIONSINSTALLED') -join ';'
    }

    # ---------- feature + component + files ----------
    $feature = $project.AddFeature('MainProgram')
    $feature.DisplayName  = 'TanbalType'
    $feature.Description  = 'TanbalType - automatic Persian/English keyboard layout correction.'
    $feature.Required     = $true
    $feature.InstallLevel = 1

    $component = $project.AddComponent('AppFiles')
    $component.Destination = '[INSTALLDIR]'
    $feature.AttachComponent($component)

    $keyFileName = $null
    foreach ($file in $payloadFiles) {
        Write-Host "  + $(Split-Path $file -Leaf)"
        $added = $component.AddFile($file)
        if ((Split-Path $file -Leaf) -eq 'TanbalType.exe') { $keyFileName = $added.Name }
    }

    # The .exe is the component's key path, so repair and shortcut resolution
    # both key off the file that actually matters.
    $component.KeyPathType = 2   # kptFile
    $component.KeyPath     = $keyFileName

    # ---------- shortcuts ----------
    # Walks the component's shortcut-destination tree to find a folder by name;
    # only leaf destinations accept shortcuts ([TaskBarFolder] is a container).
    function Find-ShortcutFolder {
        param($Folders, [string] $Name)
        foreach ($folder in $Folders) {
            if ($folder.Name -eq $Name) { return $folder }
            if ($folder.SubFolders.Count -gt 0) {
                $hit = Find-ShortcutFolder -Folders $folder.SubFolders -Name $Name
                if ($hit) { return $hit }
            }
        }
        return $null
    }

    $programMenu = Find-ShortcutFolder -Folders $component.ISWiFolders -Name '[ProgramMenuFolder]'
    if (-not $programMenu) { throw 'Could not find [ProgramMenuFolder] among the component shortcut destinations.' }

    # Shortcut names are the primary key of the MSI Shortcut table, so the two
    # shortcuts need distinct keys even though both show as "TanbalType".
    $shortcutDescription = 'TanbalType - fixes text typed with the wrong keyboard layout'

    $startMenuShortcut = $programMenu.AddShortcut('TanbalTypeStartMenu')
    $startMenuShortcut.DisplayName      = 'TanbalType'
    $startMenuShortcut.Target           = '[INSTALLDIR]TanbalType.exe'
    $startMenuShortcut.WorkingDirectory = 'INSTALLDIR'
    $startMenuShortcut.Description      = $shortcutDescription
    $startMenuShortcut.IconFile         = $appIcon
    $startMenuShortcut.IconIndex        = 0

    $desktop = Find-ShortcutFolder -Folders $component.ISWiFolders -Name '[DesktopFolder]'
    if ($desktop) {
        $desktopShortcut = $desktop.AddShortcut('TanbalTypeDesktop')
        $desktopShortcut.DisplayName      = 'TanbalType'
        $desktopShortcut.Target           = '[INSTALLDIR]TanbalType.exe'
        $desktopShortcut.WorkingDirectory = 'INSTALLDIR'
        $desktopShortcut.Description      = $shortcutDescription
        $desktopShortcut.IconFile         = $appIcon
        $desktopShortcut.IconIndex        = 0
    }

    # ---------- product configuration + release ----------
    $config = $project.AddProductConfig('TanbalType')
    # The offline build gets its own file name so both variants can sit side by
    # side in DISK1; the default (online) keeps the plain name the README points
    # users at.
    $setupSuffix = if ($BundleDotNet) { '-offline' } else { '' }
    $config.SetupFileName   = "TanbalType-$ProductVersion-setup$setupSuffix"
    $config.MSIPackageFileName = 'TanbalType'

    $release = $config.AddRelease('SingleImage')
    $release.MediaType              = 1      # same media type InstallShield's own Basic MSI sample uses
    $release.Compressed             = $true  # files inside the .msi
    $release.SetupEXE               = $true  # wrap into setup.exe so the prerequisite can run
    $release.RequiredExecutionLevel = 0  # administrator: needed to write under Program Files

    # Cosmetic: give setup.exe the app's own icon. Not worth failing the whole
    # generation over, so it is attempted and skipped if the build engine
    # refuses the path.
    try { $release.SingleEXEIconName = $appIcon }
    catch { Write-Warning "Could not set the setup.exe icon: $($_.Exception.Message)" }

    $project.SaveProject()

    # XML format keeps the project file readable and diffable in git; it opens
    # in the InstallShield IDE exactly like the binary form.
    $project.UseXMLProjectFormat = $true
    $project.SaveProject()
}
finally {
    $project.CloseProject()
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($project)
}

# ---------- setup prerequisite: .NET 8 Desktop Runtime ----------
# InstallShield's stock "Microsoft .NET 8.0 Desktop Runtime 8.0.6 (x64)" checks
# for exactly 8.0.6. A machine with 8.0.10 or 8.0.28 (which run TanbalType fine,
# since .NET rolls forward to the newest 8.0 patch) was told to install .NET 8
# anyway. So we ship our own copy of that prerequisite with the condition
# replaced: one condition per 8.0 patch, each "8.0.N is not registered".
# InstallShield installs a prerequisite only when ALL its conditions are true
# (Help: "Adding a New Installation Condition for an InstallShield
# Prerequisite"), i.e. only when no 8.0.x desktop runtime is present at all.
$stockPrq = Join-Path $InstallShieldRoot 'SetupPrerequisites\Microsoft .NET 8.0 Desktop Runtime 8.0.6 (x64).prq'
$prq = 'TanbalType .NET 8 Desktop Runtime (x64).prq'
$prqFile = Join-Path $scriptDir $prq
if (-not (Test-Path $stockPrq)) {
    Write-Warning "Stock .NET 8 prerequisite not found, skipping: $stockPrq"
}
else {
    $sharedfx = 'HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App'
    # Same shape as the stock condition (a REG_DWORD 1 per installed version);
    # 0..99 leaves plenty of room for future 8.0 servicing releases.
    $conditions = (0..99 | ForEach-Object {
        "`t`t<condition Type=`"2`" Comparison=`"32`" Path=`"$sharedfx`" FileName=`"8.0.$_`" ReturnValue=`"1`" Bits=`"2`"/>"
    }) -join "`r`n"

    $prqXml = Get-Content $stockPrq -Raw -Encoding UTF8
    $prqXml = [regex]::Replace($prqXml, '(?s)<conditions>.*?</conditions>', "<conditions>`r`n$conditions`r`n`t</conditions>")
    # Own id, and no AltPrqURL, so InstallShield never "updates" this file back
    # to the stock one from its server.
    $prqXml = $prqXml -replace 'Id="\{[0-9A-Fa-f-]+\}"', 'Id="{6B1D2C4E-8F3A-4E7B-9C51-2A7D0E9F4B36}"'
    $prqXml = $prqXml -replace '\s+AltPrqURL="[^"]*"', ''
    $prqXml = $prqXml -replace 'Description="[^"]*"', 'Description="Microsoft .NET 8.0 Desktop Runtime (x64) - installed only when no 8.0.x desktop runtime is present."'
    [IO.File]::WriteAllText($prqFile, $prqXml, (New-Object Text.UTF8Encoding $false))
    Write-Host "  wrote $prq (detects any 8.0.x)"
    # Two rows control this, and the release-level one wins:
    #   ISSetupPrerequisites.ISSetupLocation        per prerequisite
    #   ISRelease.ISSetupPrerequisiteLocation       for the whole release
    # In both, 2 means "download from the web" and 1 means "extract from
    # setup.exe".
    $location = if ($BundleDotNet) { 1 } else { 2 }

    if ($BundleDotNet) {
        # Streaming the runtime into setup.exe needs the redistributable on disk.
        # The stock .prq points inside Program Files
        # (<ISProductFolder>\SetupPrerequisites\...), where IsCmdBld -prqdownload
        # cannot write without elevation: it prints "Download Failed", then still
        # reports "Setup.exe created" and produces an installer that is missing
        # the very runtime it promises to extract. So the file is kept next to
        # this script instead, and fetched here when absent. No admin needed.
        $prqText  = [IO.File]::ReadAllText($prqFile)
        $url      = [regex]::Match($prqText, 'URL="([^"]+)"').Groups[1].Value
        $checkSum = [regex]::Match($prqText, 'CheckSum="([^"]+)"').Groups[1].Value
        $sizeText = [regex]::Match($prqText, 'FileSize="([^"]+)"').Groups[1].Value
        if (-not $url) { throw 'No download URL in the generated prerequisite.' }
        # FileSize is written "high,low"; the low word is the byte count.
        $expectedSize = [int64](($sizeText -split ',')[-1])

        $redistDir = Join-Path $scriptDir 'prereq\Microsoft .net Core\8.0.6'
        $redist    = Join-Path $redistDir (Split-Path $url -Leaf)
        if (-not (Test-Path $redist) -or (Get-Item $redist).Length -ne $expectedSize) {
            New-Item -ItemType Directory -Force -Path $redistDir | Out-Null
            Write-Host ("  downloading {0} ({1} MB)..." -f (Split-Path $url -Leaf), [math]::Round($expectedSize / 1MB))
            $ProgressPreference = 'SilentlyContinue'
            try { Invoke-WebRequest -Uri $url -OutFile $redist -UseBasicParsing }
            catch { throw "Could not download $url - put the file at $redist by hand and run again." }
        }
        $actual = (Get-FileHash $redist -Algorithm MD5).Hash
        if ($actual -ne $checkSum) { throw "Checksum mismatch for $redist (expected $checkSum, got $actual)." }

        # point the prerequisite at that copy
        $lf = [regex]::Match($prqText, 'LocalFile="([^"]*)"')
        if (-not $lf.Success) { throw 'No LocalFile attribute in the generated prerequisite.' }
        $prqText = $prqText.Remove($lf.Groups[1].Index, $lf.Groups[1].Length).Insert(
            $lf.Groups[1].Index, [Security.SecurityElement]::Escape($redist))
        [IO.File]::WriteAllText($prqFile, $prqText, (New-Object Text.UTF8Encoding $false))
        Write-Host "  runtime: $redist"
    }

    $xml = Get-Content -Path $IsmPath -Raw -Encoding UTF8

    # a) the prerequisite itself
    # <ISProjectFolder> is the folder holding TanbalType.ism, where the custom
    # .prq above was written.
    $prqRow = '<row><td>{0}</td><td>&lt;ISProjectFolder&gt;\{0}</td><td>1</td><td>{1}</td><td/></row>' -f $prq, $location
    $marker = '<col def="S255">ISReleaseFlags</col>'
    $prqTable = [regex]::Match($xml, '<table name="ISSetupPrerequisites">.*?</table>', 'Singleline')
    if (-not $prqTable.Success) { throw 'ISSetupPrerequisites table not found in the generated project.' }
    if ($prqTable.Value -notmatch [regex]::Escape($marker)) { throw 'Unexpected ISSetupPrerequisites schema.' }
    $patched = $prqTable.Value -replace [regex]::Escape($marker), ($marker + "`r`n`t`t" + $prqRow)
    $xml = $xml.Remove($prqTable.Index, $prqTable.Length).Insert($prqTable.Index, $patched)

    # b) the release. ISSetupPrerequisiteLocation is the table's last column, so
    #    it is the release row's last cell.
    $relTable = [regex]::Match($xml, '<table name="ISRelease">.*?</table>', 'Singleline')
    if (-not $relTable.Success) { throw 'ISRelease table not found in the generated project.' }
    $patchedRel = [regex]::Replace(
        $relTable.Value,
        '(<row>.*?)(<td/>|<td>\d*</td>)(</row>)',
        { param($m) $m.Groups[1].Value + "<td>$location</td>" + $m.Groups[3].Value },
        'Singleline')
    if ($patchedRel -eq $relTable.Value) { throw 'Could not set ISRelease.ISSetupPrerequisiteLocation.' }
    $xml = $xml.Remove($relTable.Index, $relTable.Length).Insert($relTable.Index, $patchedRel)

    [IO.File]::WriteAllText($IsmPath, $xml, (New-Object Text.UTF8Encoding $false))

    $how = if ($BundleDotNet) { 'bundled into setup.exe' } else { 'downloaded if missing' }
    Write-Host "  prerequisite: $prq ($how)"
}

# ---------- major upgrade ----------
# The blank template only ships the "prevent downgrade" row. Without a second
# row, installing a newer TanbalType would leave the older one behind and show
# two entries in Add/Remove Programs. This row makes Windows Installer uninstall
# every older version first. FindRelatedProducts, MigrateFeatureStates and
# RemoveExistingProducts are already sequenced in the template, so the row is
# all that is missing.
#   VersionMax = ***ALL_VERSIONS*** is replaced with the product version at
#   build time, and it is exclusive, so a build never matches itself.
#   Attributes 1 = msidbUpgradeAttributesMigrateFeatures.
$xml = Get-Content -Path $IsmPath -Raw -Encoding UTF8
if ($xml -notmatch 'TanbalTypeMajorUpgrade') {
    $upgradeRow = '<row><td>{0}</td><td></td><td>***ALL_VERSIONS***</td><td></td><td>1</td><td/><td>PREVIOUSVERSIONSINSTALLED</td><td>TanbalTypeMajorUpgrade</td></row>' -f $UpgradeCode
    $upgradeTable = [regex]::Match($xml, '<table name="Upgrade">.*?</table>', 'Singleline')
    if (-not $upgradeTable.Success) { throw 'Upgrade table not found in the generated project.' }
    $patched = $upgradeTable.Value -replace '(?s)(\s*)(</table>)', ("`r`n`t`t" + $upgradeRow + '$1$2')
    $xml = $xml.Remove($upgradeTable.Index, $upgradeTable.Length).Insert($upgradeTable.Index, $patched)
    [IO.File]::WriteAllText($IsmPath, $xml, (New-Object Text.UTF8Encoding $false))
    Write-Host '  major upgrade: older versions are removed on install'
}

Write-Host "Wrote $IsmPath"
