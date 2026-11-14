using System.Windows.Forms;

namespace Maestro.Plugin;

/// <summary>
/// Does an action on the vatSys UI thread. Do not use this class unless you must.
/// </summary>
/// <remarks>
/// <para>
/// You must use this class only if both of these conditions are true:
/// </para>
/// <list type="number">
/// <item>
/// The code can run on a thread that is not the vatSys UI thread. For example: <c>OnFDRUpdate</c> and
/// <c>OnRadarTrackUpdate</c>, the <c>Network</c> events, the MediatR handlers, the work queue, <c>Task.Run</c>, and code after
/// <c>ConfigureAwait(false)</c>. Some vatSys events (for example, <c>MMI.SelectedTrackChanged</c>) can occur on any
/// thread.
/// </item>
/// <item>
/// The code changes something that must change on the UI thread: a WinForms form or control, a WPF element (for
/// example, it makes, shows, or closes a window), or the state that a WPF view binds to (for example, a view model
/// property or collection).
/// </item>
/// </list>
/// <para>
/// Do not use this class in these cases:
/// </para>
/// <list type="bullet">
/// <item>
/// The code always runs on the UI thread. For example: WinForms and WPF event handlers, menu item clicks, form events,
/// and a <c>DispatcherTimer</c>. The action then runs immediately, thus the call does nothing. Call the code directly,
/// and catch the exceptions, because an exception that is not caught on the UI thread stops vatSys.
/// </item>
/// <item>The code does not change the UI.</item>
/// <item>A method that you call already goes to the UI thread. For example, the window manager.</item>
/// <item>
/// The UI does not change. Do the checks on the background thread first. For example, if a window shows one aircraft,
/// do not post the updates of the other aircraft.
/// </item>
/// </list>
/// <para>
/// On a thread that is not the UI thread, each call posts one message to the UI thread. vatSys and all the plugins use
/// the same UI thread, and a thread can have a maximum of 10,000 posted messages. If the queue becomes full, the WPF
/// content in vatSys can stop. Thus, do not call this method for each radar track update or each flight plan update.
/// </para>
/// </remarks>
public class GuiInvoker(Action<MethodInvoker> invoker)
{
    public void InvokeOnUiThread(Action<Form> action)
    {
        var mainForm = Application.OpenForms["MainForm"];
        if (mainForm == null)
            return;

        // If already on UI thread, execute directly to avoid deadlock
        if (!mainForm.InvokeRequired)
        {
            action(mainForm);
            return;
        }

        invoker.Invoke(delegate { action(mainForm); });
    }
}