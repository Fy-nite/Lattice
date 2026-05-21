Imports System.Collections.Generic
Imports ObjectIR.Core
Imports ObjectIR.Core.AST

Namespace Connectors
    Public Class StdlibConnector
        'Return a list of AST ClassNodes that provide
        'native implementations for standard library functions.
        Public Function GetStdlib() As List(Of ClassNode)
            Dim types As New List(Of ClassNode)()

            ' IO class
            Dim ioMethods As New List(Of MethodNode)()

            ' Print
            Dim printNative = New NativeMethod(Function(args As Value(Of Object)()) As Value(Of Object)
                                                   If args IsNot Nothing AndAlso args.Length > 0 Then
                                                       Console.Write(args(0).Data)
                                                   End If
                                                   Return New Value(Of Object)(Nothing)
                                               End Function)

            Dim printParams As New List(Of ParameterNode) From {New ParameterNode("value", TypeRef.String)}
            Dim printMethod As New MethodNode(name:="Print", parameters:=printParams, returnType:=TypeRef.Void, isStatic:=True, nativeImpl:=printNative)
            ioMethods.Add(printMethod)

            ' Println
            Dim printlnNative = New NativeMethod(Function(args As Value(Of Object)()) As Value(Of Object)
                                                     If args IsNot Nothing AndAlso args.Length > 0 Then
                                                         Console.WriteLine(args(0).Data)
                                                     Else
                                                         Console.WriteLine()
                                                     End If
                                                     Return New Value(Of Object)(Nothing)
                                                 End Function)

            Dim printlnParams As New List(Of ParameterNode) From {New ParameterNode("value", TypeRef.String)}
            Dim printlnMethod As New MethodNode(name:="Println", parameters:=printlnParams, returnType:=TypeRef.Void, isStatic:=True, nativeImpl:=printlnNative)
            ioMethods.Add(printlnMethod)

            Dim ReadlnNative = New NativeMethod(Function(args As Value(Of Object)()) As Value(Of Object)
                                                    Return New Value(Of Object)(Console.ReadLine())
                                                End Function)
            Dim ReadlnParams As New List(Of ParameterNode)
            Dim ReadlnMethod As New MethodNode(name:="Readln", parameters:=ReadlnParams, returnType:=TypeRef.String, isStatic:=True, nativeImpl:=ReadlnNative)
            ioMethods.Add(ReadlnMethod)
            ' Thread class
            Dim threadMethods As New List(Of MethodNode)()

            ' Spawn
            Dim spawnNative = New NativeMethod(Function(args As Value(Of Object)()) As Value(Of Object)
                                                   If args IsNot Nothing AndAlso args.Length > 0 AndAlso args(0).Data IsNot Nothing Then
                                                       Dim loader = ObjectIR.StdLib.Core.Memory.ProgramLoader.Current
                                                       If loader IsNot Nothing Then
                                                           ' We assume args(0).Data is an IDelagate
                                                           loader.SpawnThread(DirectCast(args(0).Data, ObjectIR.StdLib.Core.Generics.IDelagate))
                                                       End If
                                                   End If
                                                   Return New Value(Of Object)(Nothing)
                                               End Function)

            Dim spawnParams As New List(Of ParameterNode) From {New ParameterNode("entryPoint", "IDelagate")}
            Dim spawnMethod As New MethodNode(name:="Spawn", parameters:=spawnParams, returnType:=TypeRef.Void, isStatic:=True, nativeImpl:=spawnNative)
            threadMethods.Add(spawnMethod)

            ' Sleep
            Dim sleepNative = New NativeMethod(Function(args As Value(Of Object)()) As Value(Of Object)
                                                   If args IsNot Nothing AndAlso args.Length > 0 Then
                                                       System.Threading.Thread.Sleep(Convert.ToInt32(args(0).Data))
                                                   End If
                                                   Return New Value(Of Object)(Nothing)
                                               End Function)

            Dim sleepParams As New List(Of ParameterNode) From {New ParameterNode("ms", TypeRef.Int32)}
            Dim sleepMethod As New MethodNode(name:="Sleep", parameters:=sleepParams, returnType:=TypeRef.Void, isStatic:=True, nativeImpl:=sleepNative)
            threadMethods.Add(sleepMethod)

            Dim threadClass As New ClassNode("Thread", New List(Of String)(), New List(Of FieldNode)(), New List(Of ConstructorNode)(), threadMethods)
            threadClass.IsStatic = True
            types.Add(threadClass)

            Return types
        End Function

    End Class
End Namespace
