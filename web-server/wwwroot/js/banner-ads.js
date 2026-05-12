// Renders escalating sponsored "banner ads" using movie posters proxied from
// the Jurassic movie service. Stage 1 is a top banner. Dismissing it spawns
// stage 2 (left + right side banners). Dismissing either of those spawns
// stage 3 (full-screen takeover). Dismissing the takeover finally remembers
// the user's preference for ~30 minutes. If the proxy returns nothing or
// fails, no banner is shown at all.
(function () {
  'use strict';

  if (window.__lotrBannerAdsMounted) return;
  window.__lotrBannerAdsMounted = true;

  var ENDPOINT = '/api/banner-ads/movies';
  var ROTATE_MS = 6000;
  var DESKTOP_HEIGHT = 88;
  var MOBILE_HEIGHT = 76;
  var MOBILE_BREAKPOINT = 640;
  var STORAGE_KEY = 'lotr.bannerAds.dismissedAt';
  var DISMISS_TTL_MS = 30 * 60 * 1000;
  var CTA_URL = 'http://localhost:5044';
  var EXIT_MS = 350;
  var RESTART_MS = 5000;

  var posters = [];
  var stage = 'idle'; // idle | top | side | fullscreen | done

  function init() {
    if (wasRecentlyDismissed()) return;

    fetch(ENDPOINT, { credentials: 'same-origin' })
      .then(function (res) { return res.ok ? res.json() : []; })
      .then(function (data) {
        if (!Array.isArray(data) || data.length === 0) return;
        posters = data;
        showTopBanner();
      })
      .catch(function () { /* silent: no banner on failure */ });
  }

  function wasRecentlyDismissed() {
    try {
      var raw = window.sessionStorage.getItem(STORAGE_KEY);
      if (!raw) return false;
      var ts = parseInt(raw, 10);
      if (isNaN(ts)) return false;
      return Date.now() - ts < DISMISS_TTL_MS;
    } catch (e) {
      return false;
    }
  }

  function rememberDismissed() {
    try {
      window.sessionStorage.setItem(STORAGE_KEY, String(Date.now()));
    } catch (e) { /* ignore */ }
  }

  function bannerHeight() {
    return window.innerWidth <= MOBILE_BREAKPOINT ? MOBILE_HEIGHT : DESKTOP_HEIGHT;
  }

  function pickRandom(items) {
    return items[Math.floor(Math.random() * items.length)];
  }

  function pickPair() {
    var a = pickRandom(posters);
    if (posters.length === 1) return [a, a];
    var others = posters.filter(function (p) { return p !== a; });
    var b = pickRandom(others);
    return [a, b];
  }

  // ── Stage 1: top banner ─────────────────────────────────
  function showTopBanner() {
    stage = 'top';

    var banner = document.createElement('aside');
    banner.className = 'lotr-ad-banner';
    banner.setAttribute('role', 'complementary');
    banner.setAttribute('aria-label', 'Sponsored advertisement');

    var label = document.createElement('div');
    label.className = 'lotr-ad-banner__label';
    label.textContent = 'Sponsored';

    var slides = document.createElement('div');
    slides.className = 'lotr-ad-banner__slides';

    var slideEls = posters.map(function (p) {
      var s = makeTopSlide(p);
      slides.appendChild(s);
      return s;
    });

    var close = document.createElement('button');
    close.className = 'lotr-ad-banner__close';
    close.type = 'button';
    close.setAttribute('aria-label', 'Dismiss advertisement');
    close.innerHTML = '&times;';

    banner.appendChild(label);
    banner.appendChild(slides);
    banner.appendChild(close);

    document.body.insertBefore(banner, document.body.firstChild);
    applyBodyOffset(true);

    requestAnimationFrame(function () { banner.classList.add('is-visible'); });

    var activeIndex = 0;
    slideEls[0].classList.add('is-active');

    var rotateTimer = null;
    if (slideEls.length > 1) {
      rotateTimer = window.setInterval(function () {
        slideEls[activeIndex].classList.remove('is-active');
        activeIndex = (activeIndex + 1) % slideEls.length;
        slideEls[activeIndex].classList.add('is-active');
      }, ROTATE_MS);
    }

    var onResize = function () {
      if (banner.isConnected) applyBodyOffset(true);
    };
    window.addEventListener('resize', onResize);

    close.addEventListener('click', function () {
      if (rotateTimer !== null) window.clearInterval(rotateTimer);
      window.removeEventListener('resize', onResize);
      banner.classList.remove('is-visible');
      window.setTimeout(function () {
        banner.remove();
        applyBodyOffset(false);
        showSideBanners();
      }, EXIT_MS);
    });
  }

  function makeTopSlide(poster) {
    var slide = document.createElement('div');
    slide.className = 'lotr-ad-banner__slide';

    var img = document.createElement('img');
    img.className = 'lotr-ad-banner__poster';
    img.src = poster.posterUrl;
    img.alt = poster.title + ' poster';
    img.loading = 'lazy';
    img.referrerPolicy = 'no-referrer';
    img.onerror = function () { img.style.visibility = 'hidden'; };

    var copy = document.createElement('div');
    copy.className = 'lotr-ad-banner__copy';

    var kicker = document.createElement('p');
    kicker.className = 'lotr-ad-banner__kicker';
    kicker.textContent = 'Now Playing';

    var title = document.createElement('p');
    title.className = 'lotr-ad-banner__title';
    title.textContent = poster.title;

    var sub = document.createElement('p');
    sub.className = 'lotr-ad-banner__sub';
    sub.textContent = 'Brought to you by the Jurassic Movie Service';

    copy.appendChild(kicker);
    copy.appendChild(title);
    copy.appendChild(sub);

    var cta = document.createElement('a');
    cta.className = 'lotr-ad-banner__cta';
    cta.href = CTA_URL;
    cta.target = '_blank';
    cta.rel = 'noopener noreferrer';
    cta.textContent = 'Get Tickets';

    slide.appendChild(img);
    slide.appendChild(copy);
    slide.appendChild(cta);
    return slide;
  }

  // ── Stage 2: side banners ───────────────────────────────
  function showSideBanners() {
    stage = 'side';
    var pair = pickPair();
    var left = makeSideBanner('left', pair[0]);
    var right = makeSideBanner('right', pair[1]);
    document.body.appendChild(left);
    document.body.appendChild(right);

    requestAnimationFrame(function () {
      left.classList.add('is-visible');
      right.classList.add('is-visible');
    });

    var dismissed = false;
    var dismissBoth = function () {
      if (dismissed) return;
      dismissed = true;
      stage = 'transitioning';
      left.classList.remove('is-visible');
      right.classList.remove('is-visible');
      window.setTimeout(function () {
        left.remove();
        right.remove();
        showFullscreen();
      }, EXIT_MS);
    };

    left.querySelector('.lotr-ad-side__close').addEventListener('click', dismissBoth);
    right.querySelector('.lotr-ad-side__close').addEventListener('click', dismissBoth);
  }

  function makeSideBanner(side, poster) {
    var box = document.createElement('aside');
    box.className = 'lotr-ad-side lotr-ad-side--' + side;
    box.setAttribute('role', 'complementary');
    box.setAttribute('aria-label', 'Sponsored advertisement');

    var label = document.createElement('span');
    label.className = 'lotr-ad-side__label';
    label.textContent = 'Ad';

    var close = document.createElement('button');
    close.className = 'lotr-ad-side__close';
    close.type = 'button';
    close.setAttribute('aria-label', 'Dismiss advertisement');
    close.innerHTML = '&times;';

    var img = document.createElement('img');
    img.className = 'lotr-ad-side__poster';
    img.src = poster.posterUrl;
    img.alt = poster.title + ' poster';
    img.loading = 'lazy';
    img.referrerPolicy = 'no-referrer';
    img.onerror = function () { img.style.visibility = 'hidden'; };

    var title = document.createElement('p');
    title.className = 'lotr-ad-side__title';
    title.textContent = poster.title;

    var cta = document.createElement('a');
    cta.className = 'lotr-ad-side__cta';
    cta.href = CTA_URL;
    cta.target = '_blank';
    cta.rel = 'noopener noreferrer';
    cta.textContent = 'Buy Tickets';

    box.appendChild(label);
    box.appendChild(close);
    box.appendChild(img);
    box.appendChild(title);
    box.appendChild(cta);
    return box;
  }

  // ── Stage 3: fullscreen takeover ────────────────────────
  function showFullscreen() {
    stage = 'fullscreen';
    var poster = pickRandom(posters);

    var overlay = document.createElement('div');
    overlay.className = 'lotr-ad-fullscreen';
    overlay.setAttribute('role', 'dialog');
    overlay.setAttribute('aria-modal', 'true');
    overlay.setAttribute('aria-label', 'Sponsored advertisement');

    var card = document.createElement('div');
    card.className = 'lotr-ad-fullscreen__card';

    var close = document.createElement('button');
    close.className = 'lotr-ad-fullscreen__close';
    close.type = 'button';
    close.setAttribute('aria-label', 'Dismiss advertisement');
    close.innerHTML = '&times;';

    var label = document.createElement('span');
    label.className = 'lotr-ad-fullscreen__label';
    label.textContent = 'Sponsored';

    var img = document.createElement('img');
    img.className = 'lotr-ad-fullscreen__poster';
    img.src = poster.posterUrl;
    img.alt = poster.title + ' poster';
    img.loading = 'eager';
    img.referrerPolicy = 'no-referrer';
    img.onerror = function () { img.style.visibility = 'hidden'; };

    var kicker = document.createElement('p');
    kicker.className = 'lotr-ad-fullscreen__kicker';
    kicker.textContent = 'Now Playing';

    var title = document.createElement('h2');
    title.className = 'lotr-ad-fullscreen__title';
    title.textContent = poster.title;

    var sub = document.createElement('p');
    sub.className = 'lotr-ad-fullscreen__sub';
    sub.textContent = 'A message from the Jurassic Movie Service';

    var cta = document.createElement('a');
    cta.className = 'lotr-ad-fullscreen__cta';
    cta.href = CTA_URL;
    cta.target = '_blank';
    cta.rel = 'noopener noreferrer';
    cta.textContent = 'Get Tickets Now';

    card.appendChild(close);
    card.appendChild(label);
    card.appendChild(img);
    card.appendChild(kicker);
    card.appendChild(title);
    card.appendChild(sub);
    card.appendChild(cta);
    overlay.appendChild(card);
    document.body.appendChild(overlay);

    var prevOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    requestAnimationFrame(function () { overlay.classList.add('is-visible'); });

    var onKey = function (e) {
      if (e.key === 'Escape') dismiss();
    };
    document.addEventListener('keydown', onKey);

    var dismissed = false;
    function dismiss() {
      if (dismissed) return;
      dismissed = true;
      stage = 'cooldown';
      document.removeEventListener('keydown', onKey);
      overlay.classList.remove('is-visible');
      window.setTimeout(function () {
        overlay.remove();
        document.body.style.overflow = prevOverflow;
        rememberDismissed();
        // After everything is closed, wait a beat and re-launch the chain
        // from stage 1 so the cycle never truly ends. Page reload still
        // escapes via the dismissed-recently storage flag.
        window.setTimeout(function () {
          if (stage === 'cooldown') showTopBanner();
        }, RESTART_MS);
      }, EXIT_MS);
    }
    close.addEventListener('click', dismiss);
  }

  function applyBodyOffset(active) {
    var body = document.body;
    if (active) {
      body.dataset.lotrAdOriginalPaddingTop = body.dataset.lotrAdOriginalPaddingTop || (body.style.paddingTop || '');
      body.dataset.lotrAdOriginalBoxSizing = body.dataset.lotrAdOriginalBoxSizing || (body.style.boxSizing || '');
      body.style.boxSizing = 'border-box';
      body.style.paddingTop = bannerHeight() + 'px';
    } else {
      body.style.paddingTop = body.dataset.lotrAdOriginalPaddingTop || '';
      body.style.boxSizing = body.dataset.lotrAdOriginalBoxSizing || '';
      delete body.dataset.lotrAdOriginalPaddingTop;
      delete body.dataset.lotrAdOriginalBoxSizing;
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
