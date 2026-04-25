Imports Humanizer
Imports ObjectIR.AST
Namespace Core.Debugging
    Public Module RecursiveModuleDecender

        Public Sub RecursePrintMethods(methodList As List(Of MethodNode), indent As Integer)
            'For Each method As MethodDefinition In methodList
            '    Console.WriteLine($"{New String(" "c, indent * 2)}- {method.Name}")
            '    If method.Instructions.Count > 0 Then
            '        Console.WriteLine($"{New String(" "c, (indent + 1) * 2)}Instructions:")
            '    End If
            '    For Each ins In method.Instructions
            '        If TypeOf ins Is LoadConstantInstruction Then
            '            Dim loadConst As LoadConstantInstruction = CType(ins, LoadConstantInstruction)
            '            Console.WriteLine($"{New String(" "c, (indent + 2) * 2)}- LoadConstant: {loadConst.Value}")
            '        End If
            '        If TypeOf ins Is CallInstruction Then
            '            Dim callz As CallInstruction = CType(ins, CallInstruction)
            '            Console.WriteLine($"{New String(" "c, (indent + 2) * 2)}- Call: {callz.Method.Name}() -> {callz.Method.ReturnType.Name}")
            '        End If

            '    Next
            'Next
        End Sub
        Public Sub RecursePrintTypes(modl As ModuleNode, indent As Integer)
            For Each t As ClassNode In modl.Classes
                Console.WriteLine($"- {t.Name} {{ Methods: {t.Methods.Count}, Fields: {t.Fields.Count}, BaseTypes: {t.BaseTypes.Count}}}")
                For Each x As MethodNode In t.Methods
                    Console.WriteLine($"{Formatindent(indent)}- Method: {x.Name}, params: {x.Parameters.Humanize()}, isStatic?: {x.IsStatic}, implements: {x.Implements}, instructions: {x.Body.Statements.Count}")
                Next
            Next

            'For Each t As TypeDefinition In modl
            '    Console.WriteLine($"{New String(" "c, indent * 2)}- {t.Name}")
            '    If TypeOf t Is ClassDefinition Then
            '        Console.WriteLine($"{New String(" "c, (indent + 1) * 2)}Type: Class")
            '        Dim classDef As ClassDefinition = CType(t, ClassDefinition)
            '        If classDef.NestedTypes IsNot Nothing AndAlso classDef.NestedTypes.Count > 0 Then
            '            RecursePrintTypes(classDef.NestedTypes, indent + 1)
            '        End If
            '        If classDef.Methods IsNot Nothing Then
            '            Console.WriteLine($"{New String(" "c, (indent + 1) * 2)}Methods:")
            '            RecursePrintMethods(classDef.Methods, indent + 1)
            '        End If
            '    End If
            'Next
        End Sub
        Function Formatindent(ByRef indent As Integer)
            Return New String(" "c, (indent + 2) * 2)
        End Function
    End Module
End Namespace