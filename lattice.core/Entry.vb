
Imports lattice.Runtime
Imports ObjectIR.Core
Imports ObjectIR.Core.Serialization
Imports lattice.Throwables

Public Module Program
    Public CPU As CPU
    Public Sub Main(args As String())
        Try
            Dim programPath As String = Nothing
            Dim debugMode As Boolean = False

            For Each arg In args
                If arg = "--debug" Then
                    debugMode = True
                ElseIf Not arg.StartsWith("-") Then
                    programPath = arg
                End If
            Next

            If programPath Is Nothing Then
                Console.WriteLine("Usage: lattice [--debug] <program.oir>")
                Environment.Exit(1)
            End If

            CPU = New CPU()
            CPU.Debug = debugMode
            CPU.LoadProgram(programPath)
            CPU.Run()
        Catch ex As LatticeException
            Console.ForegroundColor = ConsoleColor.Red
            Console.Error.WriteLine(ex.Message)
            Console.ResetColor()
            Environment.Exit(1)
        Catch ex As Exception
            Console.ForegroundColor = ConsoleColor.DarkRed
            Console.Error.WriteLine("An unexpected error occurred:")
            Console.Error.WriteLine(ex.ToString())
            Console.ResetColor()
            Environment.Exit(1)
        End Try
    End Sub
End Module
