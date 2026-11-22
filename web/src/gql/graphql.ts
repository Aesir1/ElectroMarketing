/* eslint-disable */
/** Internal type. DO NOT USE DIRECTLY. */
type Exact<T extends { [key: string]: unknown }> = { [K in keyof T]: T[K] };
/** Internal type. DO NOT USE DIRECTLY. */
export type Incremental<T> = T | { [P in keyof T]?: P extends ' $fragmentName' | '__typename' ? T[P] : never };
import type { DocumentTypeDecoration } from '@graphql-typed-document-node/core';
/** CSV header name for each contact field; null when the file has no such column. */
export type ColumnMappingInput = {
  city?: string | null | undefined;
  country?: string | null | undefined;
  email?: string | null | undefined;
  name?: string | null | undefined;
  obtainedOn?: string | null | undefined;
  phone?: string | null | undefined;
  postalCode?: string | null | undefined;
  street?: string | null | undefined;
};

export type ConfirmDocumentUploadInput = {
  id: string;
};

export type ContactChange =
  | 'CREATED'
  | 'UNCHANGED'
  | 'UPDATED';

export type CreateCategoryInput = {
  name: string;
};

export type DeleteDocumentInput = {
  id: string;
};

export type DocumentStatus =
  | 'AVAILABLE'
  | 'PENDING';

export type ImportStatus =
  | 'DONE'
  | 'FAILED'
  | 'PENDING'
  | 'RUNNING';

export type PreviewImportInput = {
  documentId: string;
};

export type PurgeQueueInput = {
  name: string;
};

export type RenameDocumentInput = {
  fileName: string;
  id: string;
};

export type ReplaceDocumentContentInput = {
  contentType: string;
  id: string;
  sizeBytes: number;
};

export type RequestDocumentUploadInput = {
  contentType: string;
  fileName: string;
  sizeBytes: number;
};

export type StartImportInput = {
  categoryId: string;
  culture: string;
  documentId: string;
  mapping: ColumnMappingInput;
};

export type ViewerQueryVariables = Exact<{ [key: string]: never; }>;


export type ViewerQuery = { viewer: { email: string | null, permissions: Array<string> } };

export type ContactDetailQueryVariables = Exact<{
  id: string;
}>;


export type ContactDetailQuery = { contact: { id: string, name: string | null, email: string | null, phone: string | null, phoneE164: string | null, street: string | null, postalCode: string | null, city: string | null, country: string | null, obtainedOn: unknown, emailOptOut: boolean, smsOptOut: boolean, createdAtUtc: string } | null, contactHistory: { nodes: Array<{ id: string, rowNumber: number, change: ContactChange, changes: Array<{ field: string, old: string | null, new: string | null }>, importBatch: { id: string, fileName: string, documentId: string, createdAtUtc: string, createdBy: string | null, category: { name: string } | null } | null }> | null } | null };

export type DocumentsQueryVariables = Exact<{ [key: string]: never; }>;


export type DocumentsQuery = { documents: { nodes: Array<{ id: string, fileName: string, contentType: string, sizeBytes: number, status: DocumentStatus, createdAtUtc: string, downloadUrl: string | null }> | null } | null };

export type RequestDocumentUploadMutationVariables = Exact<{
  input: RequestDocumentUploadInput;
}>;


export type RequestDocumentUploadMutation = { requestDocumentUpload: { documentUploadPayload: { uploadUrl: string, document: { id: string } } | null, errors: Array<
      | { __typename: 'ForbiddenError', message: string }
      | { __typename: 'ValidationError', message: string, fields: Array<{ field: string, messages: Array<string> }> }
    > | null } };

export type ConfirmDocumentUploadMutationVariables = Exact<{
  input: ConfirmDocumentUploadInput;
}>;


export type ConfirmDocumentUploadMutation = { confirmDocumentUpload: { document: { id: string, status: DocumentStatus } | null, errors: Array<
      | { __typename: 'ConflictError', message: string }
      | { __typename: 'NotFoundError', message: string }
    > | null } };

export type RenameDocumentMutationVariables = Exact<{
  input: RenameDocumentInput;
}>;


export type RenameDocumentMutation = { renameDocument: { document: { id: string } | null, errors: Array<
      | { __typename: 'NotFoundError', message: string }
      | { __typename: 'ValidationError', message: string, fields: Array<{ field: string, messages: Array<string> }> }
    > | null } };

export type ReplaceDocumentContentMutationVariables = Exact<{
  input: ReplaceDocumentContentInput;
}>;


export type ReplaceDocumentContentMutation = { replaceDocumentContent: { documentUploadPayload: { uploadUrl: string } | null, errors: Array<
      | { __typename: 'NotFoundError', message: string }
      | { __typename: 'ValidationError', message: string, fields: Array<{ field: string, messages: Array<string> }> }
    > | null } };

export type DeleteDocumentMutationVariables = Exact<{
  input: DeleteDocumentInput;
}>;


export type DeleteDocumentMutation = { deleteDocument: { errors: Array<{ __typename: 'NotFoundError', message: string }> | null } };

export type ImportCategoriesQueryVariables = Exact<{ [key: string]: never; }>;


export type ImportCategoriesQuery = { categories: Array<{ id: string, name: string }> };

export type CreateImportCategoryMutationVariables = Exact<{
  input: CreateCategoryInput;
}>;


export type CreateImportCategoryMutation = { createCategory: { category: { id: string, name: string } | null, errors: Array<
      | { __typename: 'ConflictError', message: string }
      | { __typename: 'ValidationError', message: string, fields: Array<{ field: string, messages: Array<string> }> }
    > | null } };

export type RequestCsvUploadMutationVariables = Exact<{
  input: RequestDocumentUploadInput;
}>;


export type RequestCsvUploadMutation = { requestDocumentUpload: { documentUploadPayload: { uploadUrl: string, document: { id: string } } | null, errors: Array<
      | { __typename: 'ForbiddenError', message: string }
      | { __typename: 'ValidationError', message: string, fields: Array<{ field: string, messages: Array<string> }> }
    > | null } };

export type ConfirmCsvUploadMutationVariables = Exact<{
  input: ConfirmDocumentUploadInput;
}>;


export type ConfirmCsvUploadMutation = { confirmDocumentUpload: { errors: Array<
      | { __typename: 'ConflictError', message: string }
      | { __typename: 'NotFoundError', message: string }
    > | null } };

export type PreviewImportMutationVariables = Exact<{
  input: PreviewImportInput;
}>;


export type PreviewImportMutation = { previewImport: { csvPreview: { delimiter: string, encoding: string, headers: Array<string>, rows: Array<Array<string>>, suggestedMapping: { name: string | null, email: string | null, phone: string | null, street: string | null, postalCode: string | null, city: string | null, country: string | null, obtainedOn: string | null } } | null, errors: Array<{ __typename: 'NotFoundError', message: string }> | null } };

export type StartImportMutationVariables = Exact<{
  input: StartImportInput;
}>;


export type StartImportMutation = { startImport: { importBatch: { id: string } | null, errors: Array<
      | { __typename: 'NotFoundError', message: string }
      | { __typename: 'ValidationError', message: string, fields: Array<{ field: string, messages: Array<string> }> }
    > | null } };

export type ImportBatchesQueryVariables = Exact<{ [key: string]: never; }>;


export type ImportBatchesQuery = { importBatches: { nodes: Array<{ id: string, fileName: string, documentId: string, status: ImportStatus, culture: string, rowCount: number, createdCount: number, updatedCount: number, unchangedCount: number, skippedCount: number, errorCount: number, createdAtUtc: string, createdBy: string | null, category: { name: string } | null }> | null } | null };

export type ImportErrorsQueryVariables = Exact<{
  id: string;
}>;


export type ImportErrorsQuery = { importBatches: { nodes: Array<{ errors: Array<{ row: number, message: string }> }> | null } | null };

export type QueuesQueryVariables = Exact<{ [key: string]: never; }>;


export type QueuesQuery = { queues: Array<{ name: string, messages: number, consumers: number }> };

export type PurgeQueueMutationVariables = Exact<{
  input: PurgeQueueInput;
}>;


export type PurgeQueueMutation = { purgeQueue: { unsignedInt: number | null } };

export class TypedDocumentString<TResult, TVariables>
  extends String
  implements DocumentTypeDecoration<TResult, TVariables>
{
  __apiType?: NonNullable<DocumentTypeDecoration<TResult, TVariables>['__apiType']>;
  private value: string;
  public __meta__?: Record<string, any> | undefined;

  constructor(value: string, __meta__?: Record<string, any> | undefined) {
    super(value);
    this.value = value;
    this.__meta__ = __meta__;
  }

  override toString(): string & DocumentTypeDecoration<TResult, TVariables> {
    return this.value;
  }
}

export const ViewerDocument = new TypedDocumentString(`
    query Viewer {
  viewer {
    email
    permissions
  }
}
    `) as unknown as TypedDocumentString<ViewerQuery, ViewerQueryVariables>;
export const ContactDetailDocument = new TypedDocumentString(`
    query ContactDetail($id: UUID!) {
  contact(id: $id) {
    id
    name
    email
    phone
    phoneE164
    street
    postalCode
    city
    country
    obtainedOn
    emailOptOut
    smsOptOut
    createdAtUtc
  }
  contactHistory(contactId: $id, first: 100) {
    nodes {
      id
      rowNumber
      change
      changes {
        field
        old
        new
      }
      importBatch {
        id
        fileName
        documentId
        createdAtUtc
        createdBy
        category {
          name
        }
      }
    }
  }
}
    `) as unknown as TypedDocumentString<ContactDetailQuery, ContactDetailQueryVariables>;
export const DocumentsDocument = new TypedDocumentString(`
    query Documents {
  documents(first: 100) {
    nodes {
      id
      fileName
      contentType
      sizeBytes
      status
      createdAtUtc
      downloadUrl
    }
  }
}
    `) as unknown as TypedDocumentString<DocumentsQuery, DocumentsQueryVariables>;
export const RequestDocumentUploadDocument = new TypedDocumentString(`
    mutation RequestDocumentUpload($input: RequestDocumentUploadInput!) {
  requestDocumentUpload(input: $input) {
    documentUploadPayload {
      uploadUrl
      document {
        id
      }
    }
    errors {
      __typename
      ... on Error {
        message
      }
      ... on ValidationError {
        fields {
          field
          messages
        }
      }
    }
  }
}
    `) as unknown as TypedDocumentString<RequestDocumentUploadMutation, RequestDocumentUploadMutationVariables>;
export const ConfirmDocumentUploadDocument = new TypedDocumentString(`
    mutation ConfirmDocumentUpload($input: ConfirmDocumentUploadInput!) {
  confirmDocumentUpload(input: $input) {
    document {
      id
      status
    }
    errors {
      __typename
      ... on Error {
        message
      }
    }
  }
}
    `) as unknown as TypedDocumentString<ConfirmDocumentUploadMutation, ConfirmDocumentUploadMutationVariables>;
export const RenameDocumentDocument = new TypedDocumentString(`
    mutation RenameDocument($input: RenameDocumentInput!) {
  renameDocument(input: $input) {
    document {
      id
    }
    errors {
      __typename
      ... on Error {
        message
      }
      ... on ValidationError {
        fields {
          field
          messages
        }
      }
    }
  }
}
    `) as unknown as TypedDocumentString<RenameDocumentMutation, RenameDocumentMutationVariables>;
export const ReplaceDocumentContentDocument = new TypedDocumentString(`
    mutation ReplaceDocumentContent($input: ReplaceDocumentContentInput!) {
  replaceDocumentContent(input: $input) {
    documentUploadPayload {
      uploadUrl
    }
    errors {
      __typename
      ... on Error {
        message
      }
      ... on ValidationError {
        fields {
          field
          messages
        }
      }
    }
  }
}
    `) as unknown as TypedDocumentString<ReplaceDocumentContentMutation, ReplaceDocumentContentMutationVariables>;
export const DeleteDocumentDocument = new TypedDocumentString(`
    mutation DeleteDocument($input: DeleteDocumentInput!) {
  deleteDocument(input: $input) {
    errors {
      __typename
      ... on Error {
        message
      }
    }
  }
}
    `) as unknown as TypedDocumentString<DeleteDocumentMutation, DeleteDocumentMutationVariables>;
export const ImportCategoriesDocument = new TypedDocumentString(`
    query ImportCategories {
  categories {
    id
    name
  }
}
    `) as unknown as TypedDocumentString<ImportCategoriesQuery, ImportCategoriesQueryVariables>;
export const CreateImportCategoryDocument = new TypedDocumentString(`
    mutation CreateImportCategory($input: CreateCategoryInput!) {
  createCategory(input: $input) {
    category {
      id
      name
    }
    errors {
      __typename
      ... on Error {
        message
      }
      ... on ValidationError {
        fields {
          field
          messages
        }
      }
    }
  }
}
    `) as unknown as TypedDocumentString<CreateImportCategoryMutation, CreateImportCategoryMutationVariables>;
export const RequestCsvUploadDocument = new TypedDocumentString(`
    mutation RequestCsvUpload($input: RequestDocumentUploadInput!) {
  requestDocumentUpload(input: $input) {
    documentUploadPayload {
      uploadUrl
      document {
        id
      }
    }
    errors {
      __typename
      ... on Error {
        message
      }
      ... on ValidationError {
        fields {
          field
          messages
        }
      }
    }
  }
}
    `) as unknown as TypedDocumentString<RequestCsvUploadMutation, RequestCsvUploadMutationVariables>;
export const ConfirmCsvUploadDocument = new TypedDocumentString(`
    mutation ConfirmCsvUpload($input: ConfirmDocumentUploadInput!) {
  confirmDocumentUpload(input: $input) {
    errors {
      __typename
      ... on Error {
        message
      }
    }
  }
}
    `) as unknown as TypedDocumentString<ConfirmCsvUploadMutation, ConfirmCsvUploadMutationVariables>;
export const PreviewImportDocument = new TypedDocumentString(`
    mutation PreviewImport($input: PreviewImportInput!) {
  previewImport(input: $input) {
    csvPreview {
      delimiter
      encoding
      headers
      rows
      suggestedMapping {
        name
        email
        phone
        street
        postalCode
        city
        country
        obtainedOn
      }
    }
    errors {
      __typename
      ... on Error {
        message
      }
    }
  }
}
    `) as unknown as TypedDocumentString<PreviewImportMutation, PreviewImportMutationVariables>;
export const StartImportDocument = new TypedDocumentString(`
    mutation StartImport($input: StartImportInput!) {
  startImport(input: $input) {
    importBatch {
      id
    }
    errors {
      __typename
      ... on Error {
        message
      }
      ... on ValidationError {
        fields {
          field
          messages
        }
      }
    }
  }
}
    `) as unknown as TypedDocumentString<StartImportMutation, StartImportMutationVariables>;
export const ImportBatchesDocument = new TypedDocumentString(`
    query ImportBatches {
  importBatches(first: 50, order: [{ createdAtUtc: DESC }]) {
    nodes {
      id
      fileName
      documentId
      status
      culture
      rowCount
      createdCount
      updatedCount
      unchangedCount
      skippedCount
      errorCount
      createdAtUtc
      createdBy
      category {
        name
      }
    }
  }
}
    `) as unknown as TypedDocumentString<ImportBatchesQuery, ImportBatchesQueryVariables>;
export const ImportErrorsDocument = new TypedDocumentString(`
    query ImportErrors($id: UUID!) {
  importBatches(where: { id: { eq: $id } }) {
    nodes {
      errors {
        row
        message
      }
    }
  }
}
    `) as unknown as TypedDocumentString<ImportErrorsQuery, ImportErrorsQueryVariables>;
export const QueuesDocument = new TypedDocumentString(`
    query Queues {
  queues {
    name
    messages
    consumers
  }
}
    `) as unknown as TypedDocumentString<QueuesQuery, QueuesQueryVariables>;
export const PurgeQueueDocument = new TypedDocumentString(`
    mutation PurgeQueue($input: PurgeQueueInput!) {
  purgeQueue(input: $input) {
    unsignedInt
  }
}
    `) as unknown as TypedDocumentString<PurgeQueueMutation, PurgeQueueMutationVariables>;