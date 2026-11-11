# 拎起姿态素材

## 灰猫纯灰毛色修订

用户确认灰猫为纯灰短毛。当前灰猫素材使用内置 imagegen 按以下提示词修正。

```text
Edit target: the supplied transparent horizontal three-cat sprite sheet. Modify ONLY the coat coloration of the RIGHTMOST gray cat. Make that one cat a SOLID BLUE-GRAY British Shorthair, with uniformly plain gray short fur. Erase every dark tabby marking: chest wavy bands/necklaces, forehead M, cheek stripes, flank or back stripes, leg bands and tail rings. Replace marks with even blue-gray fur matching the surrounding unmarked coat, retaining soft broad natural volume shading and fine individual short hairs. Preserve gold amber eyes, dark gray nose, pink inner ears, white whiskers, exact face proportions and calm expression. Preserve the right cat's EXACT scruff-held suspended posture, dangling four paws and tail, nape pinch, silhouette, scale, placement and coordinates. The left tuxedo cat and middle ragdoll must remain pixel-identical to the input. Keep the original 1536x1024 canvas with three equal columns. Background must be fully transparent alpha=0 all around and between cats and between legs, without any backdrop or ground glow or shadows. This is a precise coat recolor, maintain all other image content and existing transparency.
```

使用内置 image_gen 生成，原图参考为 assets/cats.png。三列从左到右为黑白猫、布偶猫、灰猫。LiftRenderer 读取 assets/cats-lifted.png 的黑白猫和布偶猫，以及 assets/cats-lifted-gray.png 的纯灰猫。颈部抓点和源图足点由 LiftRenderer 校准。

## 初次生成提示词

Use case: identity-preserve. Asset type: transparent desktop pet sprite sheet. Reference image shows the three exact cats whose visual identities and painterly detailed fur style must be preserved. Create ONE horizontal sprite sheet with exactly THREE equal-width columns, one full-body cat centered in each column, left black tuxedo cat with pale green eyes and white bib and white paws, middle fluffy seal-bicolor ragdoll with blue eyes and white face blaze and cream/brown fur, right gray British shorthair with amber eyes. Change each cat to a gently suspended posture as if its scruff at the back of its neck is held by an invisible mouse pointer. Make a small distinct lifted tuft/pinch at the nape, visible above the shoulders behind the head, head slightly tilted forward, body hangs vertically BELOW the scruff, both front legs and both hind legs dangle relaxed downward, tail hangs loosely. Cats look calm and cute. Preserve each distinct face, fur markings, eye color and overall proportions. Clear dangling pose rather than sitting or standing. No human hands, no mouse pointer, no handles, no bowls, no text, no floor, no cast shadows. Transparent background with genuine alpha, clean antialiased fur edges, every ear paw whisker and tail fully within its own column with generous transparent gutters. All three cats the same approximate height, matching scale, full bodies from ears to dangling rear paws. Desired canvas 1536x1024. All three nape pinch points at the same relative height about one fifth down each cat, all paws near the bottom of the canvas. Single static pose for each identity.

## 透明背景整理提示词

Edit only the background of the supplied sprite sheet. Remove ALL of the dark brown black gradient background, background haze, and soft floor glows. Replace it with fully transparent pixels (alpha=0) everywhere around and between the cats, including between all legs and around all fur edges. Output a genuine RGBA PNG on transparent background. Keep all three cats pixel-faithful in their current suspended scruff-held poses, matching their exact positions, colors, facial features, fur, whiskers, tails and dangling paws. No new content or additional shadows. This is a game sprite sheet intended for direct use over arbitrary desktop backgrounds, so zero background opacity is essential.
