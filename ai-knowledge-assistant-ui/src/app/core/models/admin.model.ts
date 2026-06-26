export interface AdminOverviewDto {
  totalUsers: number;
  activeUsers: number;
  totalDocuments: number;
  indexedDocuments: number;
  failedDocuments: number;
  totalConversations: number;
  totalChatMessages: number;
  averageFeedbackRating: number | null;
}

export interface RoleDto {
  id: string;
  name: string;
  description: string;
}

export interface AdminUserDto {
  id: string;
  email: string;
  createdAt: string;
  role: RoleDto;
}

export interface AdminUserAnalyticsDto {
  totalUsers: number;
  activeUsers: number;
  newUsersLast7Days: number;
  newUsersLast30Days: number;
}

export interface AdminDocumentAnalyticsDto {
  totalDocuments: number;
  pendingDocuments: number;
  processingDocuments: number;
  indexedDocuments: number;
  failedDocuments: number;
  mostUsedDocumentsInCitations: MostCitedDocumentDto[];
  recentProcessingFailures: DocumentProcessingFailureDto[];
}

export interface MostCitedDocumentDto {
  documentId: string;
  originalFileName: string;
  citationCount: number;
}

export interface DocumentProcessingFailureDto {
  documentId: string;
  originalFileName: string;
  errorMessage: string | null;
  uploadedAt: string;
  processedAt: string | null;
}

export interface AdminChatAnalyticsDto {
  totalConversations: number;
  archivedConversations: number;
  totalChatMessages: number;
  userMessages: number;
  assistantMessages: number;
  conversationsLast7Days: number;
}

export interface AdminFeedbackAnalyticsDto {
  totalFeedback: number;
  averageRating: number | null;
  positiveFeedback: number;
  negativeFeedback: number;
  ratingBreakdown: FeedbackRatingBreakdownDto[];
}

export interface FeedbackRatingBreakdownDto {
  rating: number;
  count: number;
}
