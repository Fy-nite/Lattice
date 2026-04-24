Imports System.IO
Imports ObjectIR.Core
Imports ObjectIR.Core.IR
Imports ObjectIR.Core.Serialization
Namespace Runtime
    Public Class CPU
        Public Program As IR.Module
        Dim ModuleReader As ModuleLoader = New ModuleLoader()

        Public Sub Run()
            Console.WriteLine("Running the CPU...")
        End Sub

        Public Sub LoadProgram(path As String)
            Dim modl = ModuleReader.LoadFromText(IO.File.ReadAllText(path))
            If modl Is Nothing Then
                Throw New FileNotFoundException($"File not found: {path}, are you sure that the file exists?")
            End If
            'modl.Types.AddRange(New Connectors.StdlibConnector().GetStdlib())
            Program = modl

            RecursePrintTypes(modl.Types, 0)

        End Sub
        Sub RecursePrintMethods(methodList As List(Of MethodDefinition), indent As Integer)
            For Each method As MethodDefinition In methodList
                Console.WriteLine($"{New String(" "c, indent * 2)}- {method.Name}")
                RecursePrintMethods(methodList, indent + 1)
            Next

        End Sub
        Sub RecursePrintTypes(modl As List(Of TypeDefinition), indent As Integer)
            'For Each z As ClassDefinition In modl
            '    For Each t As MethodDefinition In z.Methods ' get the methods of the class
            '        Console.WriteLine($"{New String(" "c, indent * 2)}- {t.Name}")
            '        RecursePrintTypes(modl, indent + 1)
            '        ' this is right?
            '    Next
            'Next
            ' how would i make the prooper recursiion?
            For Each t As TypeDefinition In modl
                Console.WriteLine($"{New String(" "c, indent * 2)}- {t.Name}")
                If TypeOf t Is ClassDefinition Then
                    Dim classDef As ClassDefinition = CType(t, ClassDefinition)
                    RecursePrintTypes(classDef.NestedTypes, indent + 1)
                    RecursePrintMethods(classDef.Methods, indent + 1)
                End If
            Next
        End Sub
    End Class
End Namespace