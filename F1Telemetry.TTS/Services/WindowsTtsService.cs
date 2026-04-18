using System.Collections.Concurrent;
using System.Speech.Synthesis;
using F1Telemetry.Core.Interfaces;

namespace F1Telemetry.TTS.Services;

/// <summary>
/// Uses a dedicated Windows speech thread so that a single <see cref="SpeechSynthesizer"/> instance is created, used, and disposed on the same thread.
/// </summary>
public sealed class WindowsTtsService : ITtsService, IDisposable
{
    private readonly BlockingCollection<SpeechWorkItem> _workItems = new();
    private readonly object _settingsSync = new();
    private readonly Thread _speechThread;
    private string _voiceName = string.Empty;
    private int _volume = 100;
    private int _rate;
    private bool _disposed;

    /// <summary>
    /// Initializes a new speech service.
    /// </summary>
    public WindowsTtsService()
    {
        _speechThread = new Thread(SpeechThreadMain)
        {
            IsBackground = true,
            Name = "F1Telemetry.WindowsTts"
        };
        _speechThread.SetApartmentState(ApartmentState.STA);
        _speechThread.Start();
    }

    /// <inheritdoc />
    public void Configure(string? voiceName, int volume, int rate)
    {
        lock (_settingsSync)
        {
            ThrowIfDisposed();
            _voiceName = voiceName?.Trim() ?? string.Empty;
            _volume = Math.Clamp(volume, 0, 100);
            _rate = Math.Clamp(rate, -10, 10);
        }
    }

    /// <inheritdoc />
    public Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.CompletedTask;
        }

        ThrowIfDisposed();

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        var workItem = new SpeechWorkItem(text.Trim(), cancellationToken);

        try
        {
            _workItems.Add(workItem);
        }
        catch (InvalidOperationException)
        {
            workItem.TrySetException(new ObjectDisposedException(nameof(WindowsTtsService)));
        }

        return workItem.Completion.Task;
    }

    /// <summary>
    /// Releases the background speech worker.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _workItems.CompleteAdding();
        _speechThread.Join();
        _workItems.Dispose();
    }

    private void SpeechThreadMain()
    {
        try
        {
            using var speechSynthesizer = new SpeechSynthesizer();

            foreach (var workItem in _workItems.GetConsumingEnumerable())
            {
                if (workItem.CancellationToken.IsCancellationRequested)
                {
                    workItem.TrySetCanceled(workItem.CancellationToken);
                    continue;
                }

                try
                {
                    string voiceName;
                    int volume;
                    int rate;

                    lock (_settingsSync)
                    {
                        voiceName = _voiceName;
                        volume = _volume;
                        rate = _rate;
                    }

                    ApplyConfiguration(speechSynthesizer, voiceName, volume, rate);
                    speechSynthesizer.Speak(workItem.Text);
                    workItem.TrySetCompleted();
                }
                catch (OperationCanceledException)
                {
                    workItem.TrySetCanceled(workItem.CancellationToken);
                }
                catch (Exception ex)
                {
                    workItem.TrySetException(ex);
                }
            }
        }
        catch (Exception ex)
        {
            while (_workItems.TryTake(out var workItem))
            {
                workItem.TrySetException(new InvalidOperationException("Windows speech synthesis is unavailable.", ex));
            }
        }
    }

    private static void ApplyConfiguration(SpeechSynthesizer speechSynthesizer, string voiceName, int volume, int rate)
    {
        if (!string.IsNullOrWhiteSpace(voiceName))
        {
            try
            {
                speechSynthesizer.SelectVoice(voiceName);
            }
            catch
            {
                speechSynthesizer.SelectVoiceByHints(VoiceGender.NotSet);
            }
        }

        speechSynthesizer.Volume = volume;
        speechSynthesizer.Rate = rate;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class SpeechWorkItem
    {
        public SpeechWorkItem(string text, CancellationToken cancellationToken)
        {
            Text = text;
            CancellationToken = cancellationToken;
            Completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public string Text { get; }

        public CancellationToken CancellationToken { get; }

        public TaskCompletionSource Completion { get; }

        public void TrySetCompleted()
        {
            Completion.TrySetResult();
        }

        public void TrySetCanceled(CancellationToken cancellationToken)
        {
            Completion.TrySetCanceled(cancellationToken);
        }

        public void TrySetException(Exception exception)
        {
            Completion.TrySetException(exception);
        }
    }
}
