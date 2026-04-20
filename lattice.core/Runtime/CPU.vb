Imports System.IO
Imports ObjectIR.Core
Imports ObjectIR.Core.Serialization
Namespace Runtime
    Public Class CPU
        Public Program As IR.Module
        Dim ModuleReader As ModuleLoader = New ModuleLoader()

        Public Sub Run()
            Console.WriteLine("Running the CPU...")
        End Sub

        Public Sub LoadProgram(path As String)
            Dim modl = ModuleReader.LoadFromText(IO.File.ReadAllText(path))
            If modl Is Nothing Then
                Throw New FileNotFoundException($"File not found: {path}, are you sure that the file exists?")
            End If
            modl.Types.AddRange(New Connectors.StdlibConnector().GetStdlib())
            Program = modl
        End Sub
    End Class
End Namespace