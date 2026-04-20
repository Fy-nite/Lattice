Imports ObjectIR.Core
Imports ObjectIR.Core.IR
Imports ObjectIR.Stdlib
Imports ObjectIR.Stdlib.System
Imports ObjectIR.Stdlib.Core.Core
Namespace Connectors
    Public Class StdlibConnector
        Public Function GetStdlib() As List(Of NativeMethod)
            Dim FL As List(Of NativeMethod) = New List(Of NativeMethod)()

            Dim IO As ClassDefinition = New ClassDefinition("Stdlib.IO")
            IO.Methods.Add(New MethodDefinition("Print", TypeReference.Void) With {
                           .NativeImpl = New NativeMethod(Function(ByVal args As Value(Of Object)()) As Value(Of Object)
                                                              ObjectIR.Stdlib.System.IO.Print(CType(args(0), Object), New Value(Of Object)(args.Skip(1).Select(Function(x) CType(x, Object)).ToArray()))
                                                              Return New Value(Of Object)(Nothing)
                                                          End Function
                                                   )})
            IO.Methods.Add(New MethodDefinition("Println", TypeReference.Void) With {
                           .NativeImpl = New NativeMethod(Function(ByVal args As Value(Of Object)()) As Value(Of Object)
                                                              ObjectIR.Stdlib.System.IO.Println(CType(args(0), Object), New Value(Of Object)(args.Skip(1).Select(Function(x) CType(x, Object)).ToArray()))
                                                              Return New Value(Of Object)(Nothing)
                                                          End Function
                                                   )})
            Return FL
        End Function
    End Class
End Namespace