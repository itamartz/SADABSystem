namespace SADAB.Shared.Enums;

public enum DeploymentType
{
    Executable,      // .exe
    MsiInstaller,    // .msi
    PowerShell,      // .ps1
    BatchScript,     // .bat, .cmd
    FilesCopy        // Copy files/folders
}
