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
            Dim ioClass As New ClassNode("IO", New List(Of String)(), New List(Of FieldNode)(), New List(Of ConstructorNode)(), ioMethods)
            ioClass.IsStatic = True
            types.Add(ioClass)

            Return types
        End Function

    End Class
End Namespace
