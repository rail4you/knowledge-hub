interface Partner {
  id: string;
  name: string;
}

interface Props {
  partners: Partner[];
}

export function PartnerSection({ partners }: Props) {
  return (
    <section>
      <div className="mb-6">
        <h2 className="text-2xl font-bold text-[#1a1a1a]">参建单位</h2>
        <p className="mt-1 text-sm text-[#999]">Participating unit</p>
      </div>

      <div className="flex flex-wrap gap-3">
        {partners.map(partner => (
          <span
            key={partner.id}
            className="inline-flex items-center px-4 py-2 rounded-md border border-[#E8E8E8] bg-white text-sm text-[#666] hover:border-[#0056D2] hover:text-[#0056D2] transition-colors"
          >
            {partner.name}
          </span>
        ))}
      </div>
    </section>
  );
}
