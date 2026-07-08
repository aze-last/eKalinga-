import os
import re

views_dir = r'c:\Users\ASUS\source\repos\eKalinga-\Views'

converters_to_remove = [
    r'<helpers:StringToVisibilityConverter[^>]*/>',
    r'<helpers:NullToHiddenConverter[^>]*/>',
    r'<helpers:BooleanToBlurRadiusConverter[^>]*/>',
    r'<helpers:InverseBooleanToVisibilityConverter[^>]*/>',
    r'<BooleanToVisibilityConverter[^>]*/>'
]

for root, _, files in os.walk(views_dir):
    for f in files:
        if f.endswith('.xaml'):
            path = os.path.join(root, f)
            with open(path, 'r', encoding='utf-8') as file:
                content = file.read()
            
            modified = False
            for pattern in converters_to_remove:
                new_content = re.sub(r'[ \t]*' + pattern + r'\r?\n?', '', content)
                if new_content != content:
                    content = new_content
                    modified = True
            
            if modified:
                print(f"Modified {path}")
                with open(path, 'w', encoding='utf-8') as file:
                    file.write(content)
