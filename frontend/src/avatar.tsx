function getDiceBearAvatarUrl(name: string): string {
  const seed = encodeURIComponent(name.trim() || "Unknown");
  return `https://api.dicebear.com/9.x/lorelei-neutral/svg?seed=${seed}&radius=50&backgroundType=gradientLinear`;
}

export function NameAvatar({
  name,
  size = "md",
}: {
  name: string;
  size?: "sm" | "md";
}) {
  const displayName = name || "Unknown";
  const avatarUrl = getDiceBearAvatarUrl(displayName);

  return (
    <img
      className={`name-avatar ${size}`}
      src={avatarUrl}
      alt={`${displayName} avatar`}
      loading="lazy"
      decoding="async"
    />
  );
}
