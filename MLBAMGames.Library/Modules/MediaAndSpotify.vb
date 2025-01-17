﻿Imports System.IO
Imports System.Threading
Imports System.Windows.Forms

Namespace Modules
    Public Class MediaAndSpotify
        Inherits AdDetection
        Implements IAdModule

        Private ReadOnly _connectSleep As TimeSpan = TimeSpan.FromSeconds(5)
        Private _initialized As Boolean
        Private _stopIt As Boolean = False

        Private _spotifyId As Integer = 0
        Private Const KeyVkNextSong = 176
        Private Const KeyVkPlayPause = 179
        Private Const KeyNextSong = "^{RIGHT}"
        Private Const KeyTab = "{TAB}"
        Private Const KeyPlayPause = " "

        Private ReadOnly _spotifyPossiblePaths() = New String() {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "spotify\\spotify.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\WindowsApps\\Spotify.exe")
        }

        Private Shared _spotifyPath As String

        Public Property ForceToOpen As Boolean
        Public Property PlayNextSong As Boolean
        Public Property UseHotkeys As Boolean
        Public Property MediaControlDelay As Integer

        Public ReadOnly Property Title As AdModulesEnum = AdModulesEnum.Spotify Implements IAdModule.Title

        Public Sub New()
            For Each path As String In _spotifyPossiblePaths
                If File.Exists(path) Then
                    _spotifyPath = path
                End If
            Next
        End Sub

        Private Function SpotifyIsRunning() As Boolean
            Return Process.GetProcessesByName("spotify").Length > 0
        End Function

        Private Sub RunSpotify()
            Process.Start(_spotifyPath)
        End Sub

        Private Function SpotifyIsInstalled() As Boolean
            Return File.Exists(_spotifyPath)
        End Function

        Private Function IsSpotifyPlaying() As Boolean
            Dim spotifyAudioSession = GetAudioSession(_spotifyId)
            Return GetCurrentVolume(spotifyAudioSession) > 0.0
        End Function

        Private Sub NextSong()
            If UseHotkeys Then
                SendActionKey(KeyNextSong)
            Else
                Thread.Sleep(MediaControlDelay)
                NativeMethods.PressKey(KeyVkNextSong)
            End If
        End Sub

        Private Sub SendActionKey(Key As String)
            Dim curr? = NativeMethods.GetForegroundWindowFromHandle()
            Dim spotifyHandle? = Process.GetProcessById(_spotifyId).MainWindowHandle
            NativeMethods.SetForegroundWindowFromHandle(spotifyHandle)
            Thread.Sleep(MediaControlDelay)
            SendKeys.SendWait(KeyTab) 'to unfocus any current field on spotify
            SendKeys.SendWait(Key)
            NativeMethods.SetBackgroundWindowFromHandle(spotifyHandle)
            NativeMethods.SetForegroundWindowFromHandle(curr)
        End Sub

        Private Sub Play()
            If UseHotkeys Then
                If IsSpotifyPlaying() Then Return
                SendActionKey(KeyPlayPause)
            Else
                Thread.Sleep(MediaControlDelay)
                NativeMethods.PressKey(KeyVkPlayPause)
            End If
        End Sub

        Private Sub Pause()
            If UseHotkeys Then
                If Not IsSpotifyPlaying() Then Return
                SendActionKey(KeyPlayPause)
            Else
                Thread.Sleep(MediaControlDelay)
                NativeMethods.PressKey(KeyVkPlayPause)
            End If
        End Sub

        Private Function IsItInitialized() As Boolean
            If Not UseHotkeys Then Return _initialized
            Try
                Process.GetProcessById(_spotifyId)
            Catch ex As Exception
                ModuleSpotifyOff()
                _initialized = False
            End Try
            Return _initialized
        End Function

        Public Sub AdEnded() Implements IAdModule.AdEnded
            If Not IsItInitialized() Then Return
            If Not UseHotkeys Then Pause()
            If PlayNextSong Then NextSong()
            If UseHotkeys Then Pause()
        End Sub

        Public Sub AdStarted() Implements IAdModule.AdStarted
            If Not IsItInitialized() Then Return
            Play()
        End Sub

        Public Sub AdPlaying() Implements IAdModule.AdPlaying

        End Sub

        Public Sub Initialize() Implements IAdModule.Initialize
            If Not SpotifyIsInstalled() Then
                _stopIt = True
                ModuleSpotifyOff()
                Console.WriteLine("Warning: Ad Detection: Spotify is not installed")
            End If

            Task.Run(AddressOf ConnectLoop)

            _initialized = True
        End Sub

        Public Sub DisposeIt() Implements IAdModule.DisposeIt
            _stopIt = True
            Dispose()
        End Sub

        Private Async Function ConnectLoop() As Task
            While Not _stopIt
                Try
                    If ConnectInternal() Then Return
                Catch ex As Exception
                    _stopIt = True
                    ModuleSpotifyOff()
                    Console.WriteLine("Warning: Ad Detection: Unexpected exception connecting to Spotify: {0}", ex.Message)
                End Try
                Await Task.Delay(_connectSleep)
            End While

            If _spotifyId = 0 AndAlso Not _stopIt Then
                ModuleSpotifyOff()
                Console.WriteLine("Ad Detection: Spotify didn't connect in time, try again.")
            End If

            _initialized = True
        End Function

        Private Function ConnectInternal() As Boolean
            If Not SpotifyIsRunning() Then
                If ForceToOpen Then
                    Console.WriteLine("Ad Detection: Spotify isn't running. Trying to start it...")
                    Try
                        RunSpotify()
                    Catch ex As Exception
                        Console.WriteLine("Warning: Ad Detection: Can't start Spotify: {0}", ex.Message)
                    End Try
                    ForceToOpen = False
                End If
                Return False
            End If
            Dim proc = Process.GetProcessesByName("spotify")

            For i = 0 To proc.Count() - 1
                If proc(i).MainWindowTitle = "" Then Continue For
                _spotifyId = proc(i).Id
                Return True
            Next

            Return False
        End Function

        Private Sub ModuleSpotifyOff()
            Instance.Form.tgMedia.SetPropertyThreadSafe(Sub() Instance.Form.tgMedia.Checked = False)
        End Sub

    End Class

End Namespace