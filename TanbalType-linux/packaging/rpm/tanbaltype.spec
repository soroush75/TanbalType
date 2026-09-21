# ساخت RPM (Fedora، RHEL، openSUSE):
#   rpmbuild -tb TanbalType-linux-VERSION.tar.gz
# یا با build.sh روی سیستمی که rpmbuild دارد.
Name:           tanbaltype
Version:        @VERSION@
Release:        1
Summary:        Fixes text typed with the wrong keyboard layout (Persian <-> English)
License:        GPL-3.0-or-later
URL:            https://github.com/soroush75/TanbalType
Source0:        TanbalType-linux-%{version}.tar.gz
BuildArch:      noarch
Requires:       python3 >= 3.8
Recommends:     python3-gobject
Recommends:     (libayatana-appindicator-gtk3 or typelib-1_0-AyatanaAppIndicator3-0_1)
Recommends:     (libnotify or libnotify-tools)

%description
TanbalType watches what you type and, when a word was typed with the wrong
keyboard layout (e.g. "sghl" instead of "سلام"), replaces it with the intended
word and switches the keyboard layout. Works on X11 and Wayland (GNOME, KDE
Plasma, Cinnamon, MATE, XFCE, Sway, Hyprland, ...). On servers the
"tanbaltype convert" command converts text in the terminal.

%prep
%setup -q -n TanbalType-linux-%{version}

%build

%install
python3 tools/stage.py --root %{buildroot} --prefix /usr

%post
python3 -m compileall -q /usr/lib/tanbaltype >/dev/null 2>&1 || :
modprobe uinput >/dev/null 2>&1 || :
udevadm control --reload-rules >/dev/null 2>&1 || :
udevadm trigger --subsystem-match=misc --sysname-match=uinput >/dev/null 2>&1 || :
udevadm trigger --subsystem-match=input --action=change >/dev/null 2>&1 || :

%preun
if [ "$1" = 0 ]; then
    pkill -f "tanbaltype( run| tray|\.cli)" >/dev/null 2>&1 || :
    rm -rf /usr/lib/tanbaltype/tanbaltype/__pycache__
fi

%postun
udevadm control --reload-rules >/dev/null 2>&1 || :

%files
/usr/bin/tanbaltype
/usr/lib/tanbaltype
/usr/share/tanbaltype
/usr/share/applications/tanbaltype.desktop
/usr/share/icons/hicolor/scalable/apps/tanbaltype*.svg
/usr/share/doc/tanbaltype
/usr/lib/udev/rules.d/70-tanbaltype.rules
/usr/lib/modules-load.d/tanbaltype.conf
%config(noreplace) /etc/xdg/autostart/tanbaltype.desktop
