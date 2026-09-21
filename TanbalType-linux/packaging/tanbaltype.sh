#!/bin/sh
# اجرای TanbalType از پوشهٔ نصب (مستقل از پوشهٔ جاری)
exec python3 -c 'import sys; sys.path.insert(0, "@LIBDIR@"); from tanbaltype.cli import main; sys.exit(main())' "$@"
