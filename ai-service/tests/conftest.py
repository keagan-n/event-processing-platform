"""Test configuration.

Force the stub provider before app.main is imported, regardless of what is
set in the developer's shell environment.
"""

import os

os.environ.pop("AI_PROVIDER", None)
