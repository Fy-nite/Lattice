Imports System.IO
Imports ObjectIR.Core.AST
Imports lattice.Core
Imports lattice.Core.Debugging
Imports lattice.Throwables
Imports ObjectIR.Core
Namespace Runtime
    Public Class CPU
        Public program As ModuleNode
        Public CurrentFrame As CallStack
        Public Property Debug As Boolean = False

        Public Sub Run(args As String())
            'Console.WriteLine("Running the CPU...")
            ' looking for a static main 
            Dim programClass = program.Classes.
                FirstOrDefault(Function(c) c.Name = "Program")

            Dim main As MethodNode = Nothing

            If programClass IsNot Nothing Then
                main = programClass.Methods.
                FirstOrDefault(Function(m) m.Name = "Main")
                If main IsNot Nothing AndAlso Not main.IsStatic Then
                    Throw New EntrypointNotFoundException("Program does not contain a static main suitable for entrypoint", "Ensure your 'Main' method in the 'Program' class is marked as static.")
                End If
            End If

            If main Is Nothing Then
                Throw New EntrypointNotFoundException("Entrypoint 'Program.Main' not found", "Create a 'Program' class with a 'Main' method to serve as the entry point.")
            End If

            ExecuteMethod(main)
        End Sub

        Public Sub LoadProgram(path As String)
            program = TextIrParser.ParseModule(File.ReadAllText(path))
            If program Is Nothing Then
                Throw New FileNotFoundException($"File not found: {path}, are you sure that the file exists?")
            End If
            program.Classes.AddRange(New Connectors.StdlibConnector().GetStdlib())

            'RecursePrintTypes(program, 0)
        End Sub

        Public Sub ExecuteMethod(method As MethodNode, Optional thisObj As ManagedObject = Nothing)
            If Debug Then Console.WriteLine($"[DEBUG] Executing method: {method.Name}")

            If method.NativeImpl IsNot Nothing Then
                If Debug Then Console.WriteLine($"[DEBUG] Calling native method: {method.Name}")
                Dim argCount = method.Parameters.Count
                Dim args(argCount - 1) As Value(Of Object)
                For i As Integer = argCount - 1 To 0 Step -1
                    Dim popped = CurrentFrame.EvaluationStack.Pop()
                    If Debug Then Console.WriteLine($"[DEBUG]   Arg {i}: {popped}")
                    If TypeOf popped Is Value(Of Object) Then
                        args(i) = DirectCast(popped, Value(Of Object))
                    Else
                        args(i) = New Value(Of Object)(popped)
                    End If
                Next
                Dim result = method.NativeImpl.Method.Invoke(args)
                If method.ReturnType.Name <> "void" AndAlso result IsNot Nothing Then
                    CurrentFrame.EvaluationStack.Push(result)
                End If
                Return
            End If

            If CurrentFrame Is Nothing Then
                CurrentFrame = New CallStack(method, thisObj)
            Else
                CurrentFrame = CurrentFrame.PushFrame(method, thisObj)
            End If

            While CurrentFrame.IP < CurrentFrame.Method.Body.Statements.Count
                Dim Instruction = CurrentFrame.Method.Body.Statements(CurrentFrame.IP)
                ExecuteInstruction(Instruction)
                CurrentFrame.IP += 1
            End While

            CurrentFrame = CurrentFrame.PopFrame()
        End Sub

        Public Sub ExecuteInstruction(ins As Statement)
            If TypeOf ins Is InstructionStatement Then
                Dim instr = DirectCast(ins, InstructionStatement).Instruction
                If TypeOf instr Is SimpleInstruction Then
                    Dim simple = DirectCast(instr, SimpleInstruction)
                    Select Case simple.OpCode.ToLower()
                        Case "ldstr"
                            Dim str = simple.Operand.ToString()
                            If str.StartsWith("""") AndAlso str.EndsWith("""") Then
                                str = str.Substring(1, str.Length - 2)
                            End If
                            CurrentFrame.EvaluationStack.Push(New Value(Of Object)(str))
                        Case "ldloc"
                            CurrentFrame.EvaluationStack.Push(CurrentFrame.Locals(simple.Operand))
                        Case "stloc"
                            CurrentFrame.Locals(simple.Operand) = CurrentFrame.EvaluationStack.Pop()
                        Case "cne"
                            If CurrentFrame.EvaluationStack.Count < 2 Then
                                Throw New RuntimeException($"cne requires 2 arguments on stack, got {CurrentFrame.EvaluationStack.Count}")
                            End If
                            Dim f = CurrentFrame.EvaluationStack.Pop()
                            Dim s = CurrentFrame.EvaluationStack.Pop()

                            Dim fData As Object = f
                            Dim sData As Object = s

                            If TypeOf f Is Value(Of Object) Then fData = DirectCast(f, Value(Of Object)).Data
                            If TypeOf s Is Value(Of Object) Then sData = DirectCast(s, Value(Of Object)).Data

                            If fData IsNot Nothing AndAlso sData IsNot Nothing AndAlso fData.GetType() IsNot sData.GetType() Then
                                Throw New RuntimeException($"Both arguments must be of same type, got {fData.GetType().ToString()} for first arg, {sData.GetType().ToString()} for seccond arg but expected {fData.GetType().ToString()} for both", "R004")
                            End If

                            If Not Equals(fData, sData) Then
                                CurrentFrame.EvaluationStack.Push(New Value(Of Object)(True))
                            Else
                                CurrentFrame.EvaluationStack.Push(New Value(Of Object)(False))
                            End If
                        Case "ldarg"
                            CurrentFrame.EvaluationStack.Push(CurrentFrame.Args(simple.Operand))
                        Case "pop"
                            CurrentFrame.EvaluationStack.Pop()
                        Case "ret"
                            ' Handle return (can be more complex if returning values)
                            CurrentFrame.IP = CurrentFrame.Method.Body.Statements.Count
                        Case Else
                            Throw New OpCodeNotFoundException(simple.OpCode, CurrentFrame.ToString())
                    End Select
                ElseIf TypeOf instr Is CallInstruction Then
                    Dim callInstr = DirectCast(instr, CallInstruction)
                    Dim targetMethod = ResolveMethod(callInstr.Target)
                    If targetMethod Is Nothing Then
                        Throw New MethodResolutionException(callInstr.Target.Name, CurrentFrame.GetStackTrace())
                    End If
                    ExecuteMethod(targetMethod)
                End If
            ElseIf TypeOf ins Is IfStatement Then
                Dim ifStmt = DirectCast(ins, IfStatement)
                If EvaluateCondition(ifStmt.Condition) Then
                    ExecuteBlock(ifStmt.Then)
                ElseIf ifStmt.Else IsNot Nothing Then
                    ExecuteBlock(ifStmt.Else)
                End If
            ElseIf TypeOf ins Is WhileStatement Then
                Dim whileStmt = DirectCast(ins, WhileStatement)
                While EvaluateCondition(whileStmt.Condition)
                    ExecuteBlock(whileStmt.Body)
                End While
            ElseIf TypeOf ins Is BlockStatement Then
                ExecuteBlock(DirectCast(ins, BlockStatement))
            End If
        End Sub

        Private Sub ExecuteBlock(block As BlockStatement)
            For Each stmt In block.Statements
                ExecuteInstruction(stmt)
            Next
        End Sub

        Private Function EvaluateCondition(condition As String) As Boolean
            If condition = "stack" Then
                Dim val = CurrentFrame.EvaluationStack.Pop()
                Dim data As Object = val
                If TypeOf val Is Value(Of Object) Then
                    data = DirectCast(val, Value(Of Object)).Data
                End If

                If TypeOf data Is Boolean Then
                    Return DirectCast(data, Boolean)
                ElseIf TypeOf data Is Integer Then
                    Return DirectCast(data, Integer) <> 0
                End If
                Return data IsNot Nothing
            End If
            Return False
        End Function

        Private Function ResolveMethod(target As MethodReference) As MethodNode
            ' Basic resolution logic: look in the current program's classes
            For Each cls In program.Classes
                If cls.Name = target.DeclaringType.Name Then
                    For Each meth In cls.Methods
                        If meth.Name = target.Name Then
                            Return meth
                        End If
                    Next
                End If
            Next
            Return Nothing
        End Function

    End Class
End Namespace