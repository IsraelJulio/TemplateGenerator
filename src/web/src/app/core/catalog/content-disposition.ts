/**
 * Reads the file name out of a `Content-Disposition` header.
 *
 * The contract promises `attachment; filename="<projectName>.zip"`; the
 * RFC 5987 `filename*` form is accepted too, since a name with accents would
 * arrive that way.
 */
export function readAttachmentFileName(header: string | null): string | null {
  if (!header) {
    return null;
  }

  const encoded = /filename\*\s*=\s*[^']*'[^']*'([^;]+)/i.exec(header);
  if (encoded) {
    try {
      return decodeURIComponent(encoded[1].trim());
    } catch {
      return encoded[1].trim();
    }
  }

  const quoted = /filename\s*=\s*"([^"]*)"/i.exec(header);
  if (quoted) {
    return quoted[1];
  }

  const bare = /filename\s*=\s*([^;]+)/i.exec(header);
  return bare ? bare[1].trim() : null;
}
