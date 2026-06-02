Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
appPath = fso.GetParentFolderName(WScript.ScriptFullName) & "\\local-print-agent.exe"
shell.Run Chr(34) & appPath & Chr(34), 0, False
