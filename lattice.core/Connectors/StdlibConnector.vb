'Imports ObjectIR.Core.AST
'Imports ObjectIR.Stdlib
'Imports ObjectIR.Stdlib.System
'Imports ObjectIR.Stdlib.Core.Core
'Namespace Connectors
'    Public Class StdlibConnector
'        'Return a list of IR type definitions (classes) that provide
'        'native implementations for standard library functions. These
'        'are attached to the MethodDefinition.NativeImpl property so the
'        'runtime can invoke managed implementations directly.
'        Public Function GetStdlib() As List(Of TypeDefinition)
'            Dim types As New List(Of TypeDefinition)()

'            ' Stdlib.IO class
'            Dim ioClass As New 

'            Dim print As MethodDefinition = ioClass.DefineMethod("Print", AST.TypeReference.Void)
'            print.IsStatic = True
'            print.NativeImpl = New ObjectIR.Core.NativeMethod(Function(args As ObjectIR.Core.Value(Of Object)()) As ObjectIR.Core.Value(Of Object)
'                                                                  If args Is Nothing OrElse args.Length = 0 Then
'                                                                      Return New ObjectIR.Core.Value(Of Object)(Nothing)
'                                                                  End If
'                                                                  ' call the underlying stdlib implementation
'                                                                  ObjectIR.Stdlib.System.IO.Print(args(0))
'                                                                  Return New ObjectIR.Core.Value(Of Object)(Nothing)
'                                                              End Function)

'            Dim println As MethodDefinition = ioClass.DefineMethod("Println", AST.TypeReference.Void)
'            println.IsStatic = True
'            println.NativeImpl = New ObjectIR.Core.NativeMethod(Function(args As ObjectIR.Core.Value(Of Object)()) As ObjectIR.Core.Value(Of Object)
'                                                                    If args Is Nothing OrElse args.Length = 0 Then
'                                                                        Return New ObjectIR.Core.Value(Of Object)(Nothing)
'                                                                    End If
'                                                                    ObjectIR.Stdlib.System.IO.Println(args(0))
'                                                                    Return New ObjectIR.Core.Value(Of Object)(Nothing)
'                                                                End Function)

'            types.Add(ioClass)

'            Return types
'        End Function
'    End Class
'End Namespace