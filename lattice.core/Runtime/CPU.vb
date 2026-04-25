Imports System.IO
Imports ObjectIR.Core.AST
Imports lattice.Core.Debugging
Namespace Runtime
    Public Class CPU

        Public Sub Run()
            Console.WriteLine("Running the CPU...")
            ' looking for a static main

        End Sub

        Public Sub LoadProgram(path As String)
            Dim modl = TextIrParser.ParseModule(File.ReadAllText(path))
            If modl Is Nothing Then
                Throw New FileNotFoundException($"File not found: {path}, are you sure that the file exists?")
            End If
            'modl.Classes.AddRange(New Connectors.StdlibConnector().GetStdlib())

            RecursePrintTypes(modl, 0)

        End Sub
    End Class
End Namespace