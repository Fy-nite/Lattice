Imports System
Imports lattice.core.Runtime
Namespace lattice
    Module Program
        Dim CPU As CPU
        Sub Main(args As String())
            CPU = New CPU()
            CPU.LoadProgram("demos/test.oir")
        End Sub
    End Module
End Namespace
