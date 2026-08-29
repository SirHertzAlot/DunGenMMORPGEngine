#!/usr/bin/env python3
"""
Seed `character_parts` table from Assets/Characters/character_parts_expanded.json
Usage:
  python seed_character_parts.py --db "Host=postgres;Port=5432;Username=mmouser;Password=mmopass;Database=mmodb"
"""
import argparse
import json
import os
import psycopg2
import psycopg2.extras

def parse_args():
    p = argparse.ArgumentParser()
    p.add_argument("--db", default=os.environ.get("POSTGRES_CONNECTION_STRING",
                   "Host=postgres;Port=5432;Username=mmouser;Password=mmopass;Database=mmodb"))
    p.add_argument("--manifest", default="Assets/Characters/character_parts_expanded.json")
    return p.parse_args()

def upsert_parts(conn, parts):
    with conn.cursor() as cur:
        sql = """
        INSERT INTO character_parts
            (part_id, asset_path, gender, variant, priority, attach_bone, meta, is_sample, created_at, updated_at)
        VALUES (%(part_id)s, %(asset_path)s, %(gender)s, %(variant)s, %(priority)s, %(attach_bone)s, %(meta)s::jsonb, %(is_sample)s, NOW(), NOW())
        ON CONFLICT (part_id, asset_path) DO UPDATE SET
            gender = EXCLUDED.gender,
            variant = EXCLUDED.variant,
            priority = EXCLUDED.priority,
            attach_bone = EXCLUDED.attach_bone,
            meta = EXCLUDED.meta,
            is_sample = EXCLUDED.is_sample,
            updated_at = NOW();
        """
        psycopg2.extras.execute_batch(cur, sql, parts, page_size=200)
    conn.commit()

def main():
    args = parse_args()
    with open(args.manifest, 'r', encoding='utf-8') as f:
        manifest = json.load(f)

    rows = []
    for part in manifest.get('parts', []):
        part_id = part.get('id') or part.get('part') or part.get('partId')
        priority = part.get('priority')
        attach = part.get('attachBone') or part.get('attach_bone')
        for file in part.get('files', []):
            row = {
                'part_id': part_id,
                'asset_path': file,
                'gender': part.get('gender'),
                'variant': part.get('variant'),
                'priority': priority,
                'attach_bone': attach,
                'meta': json.dumps({k:v for k,v in part.items() if k not in ['files']}),
                'is_sample': False
            }
            rows.append(row)

    conn = psycopg2.connect(args.db)
    try:
        upsert_parts(conn, rows)
        print(f"Upserted {len(rows)} character_parts rows")
    finally:
        conn.close()

if __name__ == '__main__':
    main()
