#!/usr/bin/env bash
set -euo pipefail

# This script generates an ERD diagram from the Entity Framework DbContext
# It reads the model classes and DbContext configuration to create a VUERD JSON file
#
# Usage:
#   ./doc/dev_scripts/generate_erd.sh

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$(dirname "$SCRIPT_DIR")")"
BACKEND_DIR="${PROJECT_ROOT}/backend/OsintBackend"
MODELS_DIR="${BACKEND_DIR}/Models"
DATA_DIR="${BACKEND_DIR}/Data"
OUTPUT_FILE="${PROJECT_ROOT}/doc/osint-framework-erd.vuerd.json"

echo "Generating ERD diagram from Entity Framework model..."
echo "Project root: ${PROJECT_ROOT}"
echo "Models directory: ${MODELS_DIR}"
echo "Output file: ${OUTPUT_FILE}"

# Check if required directories exist
if [ ! -d "${MODELS_DIR}" ]; then
    echo "Error: Models directory not found: ${MODELS_DIR}" >&2
    exit 1
fi

if [ ! -d "${DATA_DIR}" ]; then
    echo "Error: Data directory not found: ${DATA_DIR}" >&2
    exit 1
fi

# Create a temporary Python script to parse C# models
TEMP_SCRIPT=$(mktemp)
cat > "${TEMP_SCRIPT}" << 'PYTHON_SCRIPT'
#!/usr/bin/env python3
import json
import re
import os
import sys
from pathlib import Path

def parse_csharp_property(line):
    """Parse a C# property declaration"""
    # Match: public Type PropertyName { get; set; }
    match = re.search(r'public\s+(\w+\??)\s+(\w+)\s*\{', line)
    if match:
        data_type = match.group(1)
        prop_name = match.group(2)
        return prop_name, data_type
    return None, None

def parse_attribute(line):
    """Parse C# attributes like [Required], [MaxLength(100)]"""
    attributes = {}

    if '[Required]' in line:
        attributes['required'] = True
    if '[Key]' in line:
        attributes['primary_key'] = True

    max_length_match = re.search(r'\[MaxLength\((\d+)\)\]', line)
    if max_length_match:
        attributes['max_length'] = int(max_length_match.group(1))

    column_type_match = re.search(r'\[Column\(TypeName\s*=\s*"([^"]+)"\)\]', line)
    if column_type_match:
        attributes['column_type'] = column_type_match.group(1)

    fk_match = re.search(r'\[ForeignKey\("([^"]+)"\)\]', line)
    if fk_match:
        attributes['foreign_key'] = fk_match.group(1)

    return attributes

def csharp_type_to_sql(csharp_type, attributes):
    """Convert C# type to SQL type"""
    # Remove nullable marker
    base_type = csharp_type.rstrip('?')

    # Check for column type override
    if 'column_type' in attributes:
        col_type = attributes['column_type'].upper()
        if col_type == 'JSON':
            return 'JSON'
        elif col_type == 'LONGTEXT':
            return 'LONGTEXT'

    # Map C# types to SQL types
    type_map = {
        'int': 'INT',
        'Int32': 'INT',
        'long': 'BIGINT',
        'Int64': 'BIGINT',
        'string': 'VARCHAR',
        'String': 'VARCHAR',
        'DateTime': 'DATETIME',
        'bool': 'BOOLEAN',
        'Boolean': 'BOOLEAN',
        'double': 'DOUBLE',
        'Double': 'DOUBLE',
        'decimal': 'DECIMAL',
        'Decimal': 'DECIMAL',
    }

    sql_type = type_map.get(base_type, 'VARCHAR')

    # Add length for VARCHAR
    if sql_type == 'VARCHAR' and 'max_length' in attributes:
        sql_type = f"VARCHAR({attributes['max_length']})"
    elif sql_type == 'VARCHAR' and 'max_length' not in attributes:
        sql_type = 'VARCHAR(255)'

    return sql_type

def parse_model_file(file_path):
    """Parse a C# model file and extract table information"""
    with open(file_path, 'r') as f:
        lines = f.readlines()

    table_name = Path(file_path).stem
    columns = []
    current_attributes = {}
    in_class = False
    class_comment = ""

    for i, line in enumerate(lines):
        line = line.strip()

        # Get class-level comment
        if '/// <summary>' in line:
            comment_line = lines[i + 1].strip()
            class_comment = re.sub(r'///\s*', '', comment_line).strip()

        # Check if we're in a class
        if re.match(r'public\s+class\s+\w+', line):
            in_class = True
            continue

        if not in_class:
            continue

        # Parse attributes
        if line.startswith('['):
            attrs = parse_attribute(line)
            current_attributes.update(attrs)

        # Parse property
        prop_name, csharp_type = parse_csharp_property(line)
        if prop_name and csharp_type:
            # Skip navigation properties (ICollection, virtual)
            if 'ICollection' in csharp_type or 'virtual' in lines[i]:
                current_attributes = {}
                continue

            sql_type = csharp_type_to_sql(csharp_type, current_attributes)
            is_nullable = '?' in csharp_type

            # Get property comment
            prop_comment = ""
            if i > 0:
                prev_line = lines[i - 1].strip()
                if '/// <summary>' in prev_line:
                    if i > 1:
                        comment_line = lines[i].strip() if '///' in lines[i] else lines[i - 1].strip()
                        prop_comment = re.sub(r'///\s*', '', comment_line).strip()
                        prop_comment = re.sub(r'<[^>]+>', '', prop_comment).strip()

            columns.append({
                'name': prop_name,
                'type': sql_type,
                'nullable': is_nullable and not current_attributes.get('required', False),
                'primary_key': current_attributes.get('primary_key', False),
                'foreign_key': current_attributes.get('foreign_key'),
                'max_length': current_attributes.get('max_length'),
                'comment': prop_comment
            })

            current_attributes = {}

    return {
        'name': table_name,
        'comment': class_comment,
        'columns': columns
    }

def parse_dbcontext_relationships(dbcontext_file):
    """Parse relationships from DbContext OnModelCreating"""
    relationships = []

    with open(dbcontext_file, 'r') as f:
        content = f.read()

    # Find HasMany/WithOne patterns
    # Pattern: .HasMany(e => e.Results).WithOne(r => r.Investigation).HasForeignKey(r => r.OsintInvestigationId)
    pattern = r'\.HasMany\(.*?=>.*?\.(\w+)\).*?\.WithOne\(.*?=>.*?\.(\w+)\).*?\.HasForeignKey\(.*?=>.*?\.(\w+)\)'
    matches = re.findall(pattern, content)

    for match in matches:
        nav_property = match[0]
        inverse_property = match[1]
        fk_column = match[2]
        relationships.append({
            'navigation_property': nav_property,
            'inverse_property': inverse_property,
            'foreign_key': fk_column
        })

    return relationships

def create_vuerd_column(col, idx):
    """Create a VUERD column object"""
    return {
        "name": col['name'],
        "comment": col.get('comment', ''),
        "dataType": col['type'],
        "default": "",
        "option": {
            "autoIncrement": col.get('primary_key', False),
            "primaryKey": col.get('primary_key', False),
            "unique": False,
            "notNull": not col.get('nullable', True)
        },
        "ui": {
            "active": False,
            "pk": col.get('primary_key', False),
            "fk": col.get('foreign_key') is not None,
            "pfk": False,
            "widthName": 60,
            "widthComment": 120,
            "widthDataType": 60,
            "widthDefault": 60
        },
        "id": f"{col['name'].lower()}_{idx}"
    }

def create_vuerd_table(table, x, y, z_index):
    """Create a VUERD table object"""
    columns = [create_vuerd_column(col, i) for i, col in enumerate(table['columns'])]

    return {
        "name": table['name'],
        "comment": table.get('comment', ''),
        "columns": columns,
        "ui": {
            "active": False,
            "left": x,
            "top": y,
            "zIndex": z_index,
            "widthName": 60,
            "widthComment": 120
        },
        "visible": True,
        "id": f"table_{table['name'].lower()}"
    }

def create_vuerd_relationship(from_table, to_table, fk_col, rel_id):
    """Create a VUERD relationship object"""
    return {
        "identification": False,
        "relationshipType": "OneN",
        "startRelationshipType": "Dash",
        "start": {
            "tableId": f"table_{from_table.lower()}",
            "columnIds": ["id"],
            "x": 0,
            "y": 0,
            "direction": "right"
        },
        "end": {
            "tableId": f"table_{to_table.lower()}",
            "columnIds": [fk_col.lower()],
            "x": 0,
            "y": 0,
            "direction": "left"
        },
        "constraintName": f"fk_{from_table.lower()}_{to_table.lower()}",
        "visible": True,
        "id": rel_id
    }

def main():
    models_dir = sys.argv[1]
    dbcontext_file = sys.argv[2]
    output_file = sys.argv[3]

    # Parse all model files
    tables = []
    for model_file in Path(models_dir).glob('*.cs'):
        # Skip non-entity files
        if model_file.stem in ['AiJobStructuredResult', 'AiJobErrorInfo', 'AiJobDebugInfo', 'OllamaHealth']:
            continue

        table = parse_model_file(model_file)
        if table['columns']:  # Only add if it has columns
            tables.append(table)

    # Parse relationships
    relationships_data = parse_dbcontext_relationships(dbcontext_file)

    # Create VUERD structure
    # Position tables in a grid
    positions = [
        (100, 100),   # OsintInvestigation
        (600, 100),   # OsintResult
        (100, 600),   # ToolExecution
        (700, 600),   # ToolFinding
        (100, 1200),  # AiJob
    ]

    vuerd_tables = []
    for i, table in enumerate(tables):
        x, y = positions[i] if i < len(positions) else (100 + (i * 500), 100)
        vuerd_tables.append(create_vuerd_table(table, x, y, i + 1))

    # Map relationships
    relationships = []
    table_map = {
        'Results': ('OsintInvestigation', 'OsintResult', 'OsintInvestigationId'),
        'ToolExecutions': ('OsintInvestigation', 'ToolExecution', 'OsintInvestigationId'),
        'AiJobs': ('OsintInvestigation', 'AiJob', 'OsintInvestigationId'),
        'Findings': ('ToolExecution', 'ToolFinding', 'ToolExecutionId'),
    }

    rel_idx = 0
    for nav_prop, (from_table, to_table, fk_col) in table_map.items():
        relationships.append(create_vuerd_relationship(from_table, to_table, fk_col, f"rel_{rel_idx}"))
        rel_idx += 1

    # Create final VUERD JSON
    vuerd_json = {
        "canvas": {
            "version": "2.2.11",
            "width": 2000,
            "height": 2000,
            "scrollTop": 0,
            "scrollLeft": 0,
            "zoomLevel": 1,
            "show": {
                "tableComment": True,
                "columnComment": True,
                "columnDataType": True,
                "columnDefault": True,
                "columnAutoIncrement": False,
                "columnPrimaryKey": True,
                "columnUnique": False,
                "columnNotNull": True,
                "relationship": True
            },
            "database": "MariaDB",
            "databaseName": "OsintFramework",
            "canvasType": "ERD",
            "language": "GraphQL",
            "tableCase": "pascalCase",
            "columnCase": "camelCase",
            "highlightTheme": "VS2015",
            "bracketType": "none",
            "setting": {
                "relationshipDataTypeSync": True,
                "relationshipOptimization": False,
                "columnOrder": [
                    "columnName",
                    "columnDataType",
                    "columnNotNull",
                    "columnUnique",
                    "columnAutoIncrement",
                    "columnDefault",
                    "columnComment"
                ]
            },
            "pluginSerializationMap": {}
        },
        "table": {
            "tables": vuerd_tables,
            "indexes": []
        },
        "memo": {
            "memos": []
        },
        "relationship": {
            "relationships": relationships
        }
    }

    # Write output
    with open(output_file, 'w') as f:
        json.dump(vuerd_json, f, indent=2)

    print(f"✓ Generated ERD with {len(vuerd_tables)} tables and {len(relationships)} relationships")
    print(f"✓ Output written to: {output_file}")

if __name__ == '__main__':
    main()
PYTHON_SCRIPT

chmod +x "${TEMP_SCRIPT}"

# Run the Python script
python3 "${TEMP_SCRIPT}" "${MODELS_DIR}" "${DATA_DIR}/OsintDbContext.cs" "${OUTPUT_FILE}"

# Clean up
rm "${TEMP_SCRIPT}"

echo ""
echo "ERD generation complete!"
echo "Open ${OUTPUT_FILE} with VS Code ERD Editor extension or https://www.erdcloud.com/"
