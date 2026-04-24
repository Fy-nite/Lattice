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
            ' looking for a static main



        End Sub

        Public Sub LoadProgram(path As String)
            Dim modl = ModuleReader.LoadFromText(IO.File.ReadAllText(path))
            If modl Is Nothing Then
                Throw New FileNotFoundException($"File not found: {path}, are you sure that the file exists?")
            End If
            'modl.Types.AddRange(New Connectors.StdlibConnector().GetStdlib())
            Program = modl

            RecursePrintTypes(modl.Types, 0)
            Console.WriteLine(Program.Serialize().DumpToIRCode())

        End Sub
        Sub RecursePrintMethods(methodList As List(Of MethodDefinition), indent As Integer)
            For Each method As MethodDefinition In methodList
                Console.WriteLine($"{New String(" "c, indent * 2)}- {method.Name}")
                If method.Instructions.Count > 0 Then
                    Console.WriteLine($"{New String(" "c, (indent + 1) * 2)}Instructions:")
                End If
                For Each ins In method.Instructions
                    If TypeOf ins Is LoadConstantInstruction Then
                        Dim loadConst As LoadConstantInstruction = CType(ins, LoadConstantInstruction)
                        Console.WriteLine($"{New String(" "c, (indent + 2) * 2)}- LoadConstant: {loadConst.Value}")
                    End If
                Next
            Next
        End Sub
        Sub RecursePrintTypes(modl As List(Of TypeDefinition), indent As Integer)
            For Each t As TypeDefinition In modl
                Console.WriteLine($"{New String(" "c, indent * 2)}- {t.Name}")
                If TypeOf t Is ClassDefinition Then
                    Console.WriteLine($"{New String(" "c, (indent + 1) * 2)}Type: Class")
                    Dim classDef As ClassDefinition = CType(t, ClassDefinition)
                    If classDef.NestedTypes IsNot Nothing AndAlso classDef.NestedTypes.Count > 0 Then
                        RecursePrintTypes(classDef.NestedTypes, indent + 1)
                    End If
                    If classDef.Methods IsNot Nothing Then
                        Console.WriteLine($"{New String(" "c, (indent + 1) * 2)}Methods:")
                        RecursePrintMethods(classDef.Methods, indent + 1)
                    End If
                End If
            Next
        End Sub
    End Class
End Namespace