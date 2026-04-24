
Imports lattice.Runtime
Imports ObjectIR.Core
Imports ObjectIR.Core.Serialization
Public Module Program
    Public CPU As CPU
    Public Sub Main(args As String())
        Dim thing As ModuleData
        Console.WriteLine(thing)
        CPU = New CPU()
        CPU.LoadProgram("demos/test.oir")
        CPU.Run()
    End Sub
End Module
