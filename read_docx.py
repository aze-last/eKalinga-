import sys
import zipfile
import xml.etree.ElementTree as ET

def read_docx(file_path):
    try:
        with zipfile.ZipFile(file_path) as docx:
            xml_content = docx.read('word/document.xml')
            root = ET.fromstring(xml_content)
            
            # Namespaces
            ns = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
            
            paragraphs = []
            for para in root.iter('{http://schemas.openxmlformats.org/wordprocessingml/2006/main}p'):
                text = ''.join(node.text for node in para.iter('{http://schemas.openxmlformats.org/wordprocessingml/2006/main}t') if node.text)
                if text:
                    paragraphs.append(text)
            
            print("\n".join(paragraphs))
    except Exception as e:
        print(f"Failed to read docx: {e}", file=sys.stderr)

if __name__ == '__main__':
    path = r"C:\Users\ASUS\Downloads\1784010365741.docx"
    read_docx(path)
