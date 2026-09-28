document.addEventListener('DOMContentLoaded', () => {
  const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  const finePointer = window.matchMedia('(min-width: 768px) and (hover: hover) and (pointer: fine)');

  if (!reduceMotion.matches) {
    document.documentElement.classList.add('motion-ready');

    requestAnimationFrame(() => {
      document.querySelectorAll('[data-hero-reveal]').forEach((element) => {
        element.classList.add('is-visible');
      });
    });

    const revealObserver = new IntersectionObserver((entries, observer) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return;
        entry.target.classList.add('is-visible');
        observer.unobserve(entry.target);
      });
    }, { threshold: 0.16, rootMargin: '0px 0px -7% 0px' });

    document.querySelectorAll('[data-reveal]').forEach((element) => {
      revealObserver.observe(element);
    });

    const sectionObserver = new IntersectionObserver((entries, observer) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return;
        entry.target.classList.add('is-motion-visible');
        observer.unobserve(entry.target);
      });
    }, { threshold: 0.18, rootMargin: '0px 0px -8% 0px' });

    document.querySelectorAll('.workflow-section, [data-status-motion]').forEach((element) => {
      sectionObserver.observe(element);
    });

    const hero = document.querySelector('.hero-section');
    const heroProduct = document.querySelector('[data-pointer-parallax]');
    let pointerFrame = 0;

    const resetPointerDepth = () => {
      if (!heroProduct) return;
      heroProduct.classList.remove('is-tracking');
      heroProduct.style.setProperty('--pointer-x', '0px');
      heroProduct.style.setProperty('--pointer-y', '0px');
    };

    hero?.addEventListener('pointermove', (event) => {
      if (!finePointer.matches || !heroProduct || pointerFrame) return;
      pointerFrame = requestAnimationFrame(() => {
        const bounds = hero.getBoundingClientRect();
        const x = ((event.clientX - bounds.left) / bounds.width - 0.5) * 12;
        const y = ((event.clientY - bounds.top) / bounds.height - 0.5) * 8;
        heroProduct.classList.add('is-tracking');
        heroProduct.style.setProperty('--pointer-x', `${x.toFixed(2)}px`);
        heroProduct.style.setProperty('--pointer-y', `${y.toFixed(2)}px`);
        pointerFrame = 0;
      });
    }, { passive: true });

    hero?.addEventListener('pointerleave', resetPointerDepth, { passive: true });

    const scrollParallax = document.querySelector('[data-scroll-parallax]');
    let scrollFrame = 0;

    const updateScrollDepth = () => {
      if (!scrollParallax || !finePointer.matches) {
        scrollParallax?.style.setProperty('--scroll-y', '0px');
        scrollFrame = 0;
        return;
      }

      const bounds = scrollParallax.getBoundingClientRect();
      const viewportCenter = window.innerHeight / 2;
      const elementCenter = bounds.top + bounds.height / 2;
      const offset = Math.max(-6, Math.min(6, (viewportCenter - elementCenter) * 0.018));
      scrollParallax.style.setProperty('--scroll-y', `${offset.toFixed(2)}px`);
      scrollFrame = 0;
    };

    const requestScrollDepth = () => {
      if (scrollFrame) return;
      scrollFrame = requestAnimationFrame(updateScrollDepth);
    };

    window.addEventListener('scroll', requestScrollDepth, { passive: true });
    window.addEventListener('resize', requestScrollDepth, { passive: true });
    requestScrollDepth();
  }

  const toggle = document.querySelector('[data-chat-toggle]');
  const panel = document.querySelector('#chatPanel');
  const closeButtons = document.querySelectorAll('[data-chat-close]');
  const form = document.querySelector('[data-chat-form]');
  const setChat = (isOpen) => {
    panel.hidden = !isOpen;
    toggle.setAttribute('aria-expanded', String(isOpen));
    if (isOpen) panel.querySelector('input')?.focus();
  };
  toggle?.addEventListener('click', () => setChat(panel.hidden));
  closeButtons.forEach((button) => button.addEventListener('click', () => setChat(false)));
  form?.addEventListener('submit', (event) => event.preventDefault());
  if (toggle && panel && window.matchMedia('(min-width: 1024px)').matches) setChat(true);
});
