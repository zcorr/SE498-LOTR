// Renders a sponsored "banner ad" strip across the top of the page using
// movie posters proxied from the Jurassic movie service. Mounts itself once
// per page; if the proxy returns nothing or fails, no banner is shown.
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
  var DISMISS_TTL_MS = 30 * 60 * 1000; // 30 minutes

  function init() {
    if (wasRecentlyDismissed()) return;

    fetch(ENDPOINT, { credentials: 'same-origin' })
      .then(function (res) { return res.ok ? res.json() : []; })
      .then(function (posters) {
        if (!Array.isArray(posters) || posters.length === 0) return;
        mount(posters);
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

  function makeSlide(poster) {
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
    cta.href = 'http://localhost:5044';
    cta.target = '_blank';
    cta.rel = 'noopener noreferrer';
    cta.textContent = 'Get Tickets';

    slide.appendChild(img);
    slide.appendChild(copy);
    slide.appendChild(cta);
    return slide;
  }

  function mount(posters) {
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
      var s = makeSlide(p);
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

    close.addEventListener('click', function () {
      if (rotateTimer !== null) window.clearInterval(rotateTimer);
      banner.classList.remove('is-visible');
      rememberDismissed();
      window.setTimeout(function () {
        banner.remove();
        applyBodyOffset(false);
      }, 350);
    });

    window.addEventListener('resize', function () {
      if (banner.isConnected) applyBodyOffset(true);
    });
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
