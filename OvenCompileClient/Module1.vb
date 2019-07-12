Imports System.IO.Packaging
Module Module1
    Public MapPath, ZipPath, ServerIP, Email, CSGargs, BSPargs, VISargs, RADargs, RADfile, Engine As String
    Public Samba As Boolean = False
    Function GetStartupArguments()
        Dim argl As New Dictionary(Of String, String)
        Dim argsingle As New List(Of String)
        Dim arg = System.Environment.GetCommandLineArgs
        Dim hasargs = False
        If arg.Length > 1 Then
            hasargs = True
        End If
        Dim key As String = ""
        Dim value As String = ""
        For i As Integer = 0 To arg.Length - 1
            Dim temp = arg(i)
            If temp.StartsWith("-") Then
                key = temp
                argsingle.Add(key.ToLower)
            ElseIf key.Length > 0 Then
                value = temp
                If argl.ContainsKey(key) Then
                    argl(key) = value
                Else
                    argl.Add(key, value)
                End If
                key = ""
            End If
        Next
        If hasargs = True Then
            If argsingle.Contains("-samba") Then
                Samba = True
            End If
            If argsingle.Contains("-hl1") Then
                Engine = "goldsrc"
            ElseIf argsingle.Contains("-hl2") Then
                Engine = "source"
            ElseIf argsingle.Contains("-bond") Then
                Engine = "bond"
            Else
                CriticalError("ERROR: Game Engine argument not set! Please specify -hl1, -hl2, or -bond")
            End If
            argl.TryGetValue("-map", MapPath)
            argl.TryGetValue("-ip", ServerIP)
            argl.TryGetValue("-email", Email)
            If MapPath = Nothing Then
                CriticalError("ERROR: -map command line argument not set!")
                Return False
            End If
            If Email = Nothing Then
                CriticalError("ERROR: -email command line argument not set!")
                Return False
            End If
            If ServerIP = Nothing Then
                Try
                    Dim dns As System.Net.IPHostEntry = System.Net.Dns.GetHostEntry("click4dylan.no-ip.org")
                    ServerIP = dns.AddressList(0).ToString
                Catch
                    Return False
                End Try
                If Not ServerIP = Nothing Then
                    Return True
                End If
            End If
        Else
            CriticalError("ERROR: You need to specify command line arguments -map mapname.map and -email youremail@host.com to use this program. You can also set the server IP with -ip and use the Samba protocol with -samba instead of FTP")
        End If
        Return False
    End Function
    Sub CriticalError(ByVal message As String)
        Console.WriteLine(message)
        Threading.Thread.Sleep(8000)
    End Sub
    Sub Main()
        If GetStartupArguments() Then
            If ParseMapFile() Then
                If CreateINIFile() Then
                    If UploadFiles() Then
                        Console.WriteLine("Oven job created. You should receive an email when the job is submitted to the first available oven.")
                        System.Media.SystemSounds.Exclamation.Play()
                        Threading.Thread.Sleep(5000)
                        'MsgBox("Oven job created. You should receive an email when the job is submitted to the first available oven.", MsgBoxStyle.Exclamation)
                    End If
                    DeleteTemporaryFiles()
                End If
            End If
        End If
    End Sub
    Function UploadFiles()
        Dim MapName As String = System.IO.Path.GetFileName(MapPath)
        Dim RADName As String = System.IO.Path.GetFileName(RADfile)
        Dim extension As String = System.IO.Path.GetExtension(MapPath)
        'Dim Files() As String = IO.Directory.GetFiles("\\96.254.179.18\uploads")
        If Samba Then
            If IO.File.Exists("\\" & ServerIP & "\uploads\" & MapName & ".ini") Then
                Try
                    IO.File.Delete("\\" & ServerIP & "\uploads\" & MapName & ".ini")
                Catch ex As Exception
                    CriticalError("FAILED TO DELETE " & MapName & ".ini" & " FROM SERVER! REASON: " & ex.Message)
                    Return False
                End Try
            End If
            If IO.File.Exists("\\" & ServerIP & "\uploads\" & RADName) Then
                Try
                    IO.File.Delete("\\" & ServerIP & "\uploads\" & RADName)
                Catch ex As Exception
                    CriticalError("FAILED TO DELETE " & RADName & " FROM SERVER! REASON: " & ex.Message)
                    Return False
                End Try
            End If
            If IO.File.Exists("\\" & ServerIP & "\uploads\" & MapName & ".rdy") Then
                Try
                    IO.File.Delete("\\" & ServerIP & "\uploads\" & MapName & ".rdy")
                Catch ex As Exception
                    CriticalError("FAILED TO DELETE " & MapName & ".rdy" & " FROM SERVER! REASON: " & ex.Message)
                    Return False
                End Try
            End If
            If IO.File.Exists("\\" & ServerIP & "\uploads\" & MapName) Then
                Try
                    IO.File.Delete("\\" & ServerIP & "\uploads\" & MapName)
                Catch ex As Exception
                    CriticalError("FAILED TO DELETE " & MapName & extension & " FROM SERVER! REASON: " & ex.Message)
                    Return False
                End Try
            End If
            Try
            Catch ex As Exception
                IO.File.Copy(MapPath, "\\" & ServerIP & "\uploads\" & MapName)
                IO.File.Copy(MapPath & ".ini", "\\" & ServerIP & "\uploads\" & MapName & ".ini")
                IO.File.Copy(RADfile, "\\" & ServerIP & "\uploads\" & RADName)
                IO.File.Copy(MapPath & ".rdy", "\\" & ServerIP & "\uploads\" & MapName & ".rdy")
                CriticalError("FAILED TO UPLOAD FILE TO SAMBA FOLDER! REASON: " & ex.Message)
                Return False
            End Try
        Else
            'Use FTP instead
            Try
                Dim instance As New System.Net.WebClient
                instance.Credentials = New System.Net.NetworkCredential("ovendev", "ovendev")
                'instance.UploadFile("ftp://" & ServerIP & "/" & MapName, MapPath)
                ZipUpMAP()
                instance.UploadFile("ftp://" & ServerIP & "/" & MapName & ".zip", MapPath & ".zip")
                instance.UploadFile("ftp://" & ServerIP & "/" & MapName & ".ini", MapPath & ".ini")
                instance.UploadFile("ftp://" & ServerIP & "/" & RADName, RADfile)
                instance.UploadFile("ftp://" & ServerIP & "/" & MapName & ".rdy", MapPath & ".rdy")
            Catch ex As Exception
                CriticalError("FAILED TO UPLOAD FILE TO THE FTP SERVER! REASON: " & ex.Message)
                Return False
            End Try
        End If

        Return True
    End Function
    Sub ZipUpMAP()
        ZipPath = MapPath & ".zip"
        If IO.File.Exists(zipPath) Then
            Try
                IO.File.Delete(zipPath)
            Catch : End Try
        End If

        Dim zip As Package = ZipPackage.Open(zipPath, IO.FileMode.Create, IO.FileAccess.ReadWrite)
        AddToArchive(zip, MapPath)
        zip.Close()
    End Sub
    Private Sub AddToArchive(ByVal zip As Package, ByVal fileToAdd As String)
        'taken from http://www.codeproject.com/Articles/28107/Zip-Files-Easy

        'Replace spaces with an underscore (_) 
        Dim uriFileName As String = fileToAdd.Replace(" ", "_")

        'A Uri always starts with a forward slash "/" 
        Dim zipUri As String = String.Concat("/", IO.Path.GetFileName(uriFileName))

        Dim partUri As New Uri(zipUri, UriKind.Relative)
        Dim contentType As String = Net.Mime.MediaTypeNames.Application.Zip

        'The PackagePart contains the information: 
        ' Where to extract the file when it's extracted (partUri) 
        ' The type of content stream (MIME type):  (contentType) 
        ' The type of compression:  (CompressionOption.Normal)   
        Dim pkgPart As PackagePart = zip.CreatePart(partUri, contentType, CompressionOption.Normal)

        'Read all of the bytes from the file to add to the zip file 
        Dim bites As Byte() = IO.File.ReadAllBytes(fileToAdd)

        'Compress and write the bytes to the zip file 
        pkgPart.GetStream().Write(bites, 0, bites.Length)


    End Sub
    Sub DeleteTemporaryFiles()
        If IO.File.Exists(MapPath & ".ini") Then
            Try
                IO.File.Delete(MapPath & ".ini")
            Catch
            End Try
        End If
        If IO.File.Exists(MapPath & ".rdy") Then
            Try
                IO.File.Delete(MapPath & ".rdy")
            Catch
            End Try
        End If
        If IO.File.Exists(ZipPath) Then
            Try
                IO.File.Delete(ZipPath)
            Catch
            End Try
        End If
    End Sub
    Function CreateINIFile()
        Dim MapName As String = System.IO.Path.GetFileName(MapPath)
        Dim extension As String = System.IO.Path.GetExtension(MapPath)
        Dim directory As String = System.IO.Path.GetDirectoryName(MapPath)
        Dim inistring As String = "[map]" & vbLf & "mapfile=" & MapName & _
            vbLf & "radfile=" & RADfile & vbLf & "product=" & Engine & vbLf & _
            "gamedir=" & Engine & vbLf & "csg=" & CSGargs & vbLf & "bsp=" & _
            BSPargs & vbLf & "vis=" & VISargs & vbLf & "rad=" & RADargs & _
            vbLf & "mailto=" & Email & vbLf
        If RADfile = Nothing Then
            If Not IO.File.Exists(MapPath.Replace(extension, ".rad")) Then
                Try
                    RADfile = MapPath.Replace(extension, ".rad")
                    IO.File.WriteAllText(MapPath.Replace(extension, ".rad"), Nothing)
                Catch ex As Exception
                    CriticalError("ERROR: Failed to write RAD file! Reason: " & ex.Message)
                End Try
            End If
        Else
            RADfile = directory & "\" & RADfile
        End If
        If Not IO.File.Exists(RADfile) Then
            Try
                RADfile = MapPath.Replace(extension, ".rad")
                IO.File.WriteAllText(MapPath.Replace(extension, ".rad"), Nothing)
            Catch ex As Exception
                CriticalError("ERROR: Failed to write RAD file! Reason: " & ex.Message)
            End Try
        End If
        Try
            IO.File.WriteAllText(MapPath & ".ini", inistring)
            IO.File.WriteAllText(MapPath & ".rdy", Nothing)
        Catch ex As Exception
            CriticalError("ERROR: Failed to write INI file! Reason: " & ex.Message)
            Return False
        End Try
        Return True
    End Function
    Function ParseMapFile()
        If Not IO.File.Exists(MapPath) Then
            CriticalError(MapPath & " does not exist!")
            Return False
        End If

        Dim sr As New IO.StreamReader(MapPath)

        Dim state_brace As Integer = 0
        Dim state_bracket As Integer = 0
        Dim state_quote As Boolean = False
        Dim state_quoteclosure As Integer = 0
        Dim state_line As Integer = 0
        Dim state_buffer As String = ""
        Dim state_collection As New List(Of String)
        Dim state_blockno As Integer = 0

        Dim state_output As New Dictionary(Of String, String)

        While sr.Peek > 0
            Dim a = sr.ReadLine
            state_quoteclosure = 0
            state_buffer = ""
            state_collection.Clear()

            For Each b In a
                Select Case b
                    Case ControlChars.Quote
                        If state_quote = True Then
                            state_quoteclosure += 1
                            state_collection.Add(state_buffer)
                            state_buffer = ""
                        End If
                        state_quote = Not state_quote
                        Exit Select
                    Case "{"
                        state_blockno += 1
                        state_brace += 1
                        Exit Select
                    Case "}"
                        state_brace -= 1
                        Exit Select
                    Case Else
                        If state_quote Then
                            state_buffer &= b
                        End If
                        Exit Select
                End Select
            Next

            If state_collection.Count = 2 And state_blockno = 1 Then
                Try
                    state_output.Add(state_collection(0), state_collection(1))
                Catch
                End Try
            End If

            ' Exit once finished parsing first block
            If state_blockno > 1 Then
                Exit While
            End If

            state_line += 1
        End While

        If state_output.ContainsKey("csg_options") Then
            CSGargs = state_output("csg_options")
        End If
        If state_output.ContainsKey("bsp_options") Then
            BSPargs = state_output("bsp_options")
        End If
        If state_output.ContainsKey("vis_options") Then
            VISargs = state_output("vis_options")
        End If
        If state_output.ContainsKey("rad_options") Then
            RADargs = state_output("rad_options")
        End If
        If state_output.ContainsKey("radfile") Then
            RADfile = state_output("radfile")
        End If
        Return True
    End Function


End Module
