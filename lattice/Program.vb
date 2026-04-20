Imports System

Module Program
    Dim CPU As core.CPU = New core.CPU()
    Sub Main(args As String())
        CPU.LoadProgram("demos/test.oir")
    End Sub
End Module
