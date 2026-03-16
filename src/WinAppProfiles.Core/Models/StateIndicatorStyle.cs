namespace WinAppProfiles.Core.Models;

public enum StateIndicatorStyle
{
    PillWithArrow = 0,  // Option A: prominent current-state text + subtle desired-state arrow below
    StackedLabels = 1,  // Option B: two labelled rows "Now / Want" with dots + text
    SizedDots = 2       // Option C: small outline ring (desired) + arrow + large solid dot (current)
}
