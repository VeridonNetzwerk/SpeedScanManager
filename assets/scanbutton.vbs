Set WshShell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
exePath = fso.BuildPath(fso.GetParentFolderName(WScript.ScriptFullName), "SpeedScanManager.exe")
WshShell.Run """" & exePath & """" & " /scanbutton" & BuildArgs(), 0, False

Function BuildArgs()
    Dim args, i
    args = ""
    For i = 0 To WScript.Arguments.Count - 1
        args = args & " " & WScript.Arguments(i)
    Next
    BuildArgs = args
End Function
