Imports ObjectIR.Core
Imports ObjectIR.Core.IR
Imports ObjectIR.Stdlib
Imports ObjectIR.Stdlib.System
Imports ObjectIR.Stdlib.Core.Core
Namespace Connectors
    Public Class StdlibConnector
        'Return a list of IR type definitions (classes) that provide
        'native implementations for standard library functions. These
        'are attached to the MethodDefinition.NativeImpl property so the
        'runtime can invoke managed implementations directly.
        Public Function GetStdlib() As List(Of ObjectIR.Core.IR.TypeDefinition)
            Dim types As New List(Of ObjectIR.Core.IR.TypeDefinition)()

            ' Stdlib.IO class
            Dim ioClass As New ObjectIR.Core.IR.ClassDefinition("Stdlib.IO")

            Dim print As ObjectIR.Core.IR.MethodDefinition = ioClass.DefineMethod("Print", ObjectIR.Core.IR.TypeReference.Void)
            print.IsStatic = True
            print.NativeImpl = New ObjectIR.Core.NativeMethod(Function(args As ObjectIR.Core.Value(Of Object)()) As ObjectIR.Core.Value(Of Object)
                                                                   If args Is Nothing OrElse args.Length = 0 Then
                                                                       Return New ObjectIR.Core.Value(Of Object)(Nothing)
                                                                   End If
                                                                   ' call the underlying stdlib implementation
                                                                   ObjectIR.Stdlib.System.IO.Print(args(0))
                                                                   Return New ObjectIR.Core.Value(Of Object)(Nothing)
                                                               End Function)

            Dim println As ObjectIR.Core.IR.MethodDefinition = ioClass.DefineMethod("Println", ObjectIR.Core.IR.TypeReference.Void)
            println.IsStatic = True
            println.NativeImpl = New ObjectIR.Core.NativeMethod(Function(args As ObjectIR.Core.Value(Of Object)()) As ObjectIR.Core.Value(Of Object)
                                                                       If args Is Nothing OrElse args.Length = 0 Then
                                                                           Return New ObjectIR.Core.Value(Of Object)(Nothing)
                                                                       End If
                                                                       ObjectIR.Stdlib.System.IO.Println(args(0))
                                                                       Return New ObjectIR.Core.Value(Of Object)(Nothing)
                                                                   End Function)

            types.Add(ioClass)

            Return types
        End Function
    End Class
End Namespace